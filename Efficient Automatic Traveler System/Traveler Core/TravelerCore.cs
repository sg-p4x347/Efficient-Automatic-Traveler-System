using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Odbc;
using System.Diagnostics;
using System.IO;

namespace Efficient_Automatic_Traveler_System
{
    // Class: Used to generate and store the digital "travelers" that are used throughout the system
    // Developer: Gage Coates
    // Date started: 1/25/17

    interface ITravelerCore
    {
        void CreateScrapChild(Traveler parent, int qtyScrapped);
        Traveler CreateCompletedChild(Traveler parent, int qtyMade, double time);
        Order FindOrder(string orderNo);
        Traveler FindTraveler(int ID);
        void AdvanceTraveler(Traveler traveler);
        void RemoveTraveler(Traveler traveler);
        List<Traveler> GetTravelers
        {
            get;
        }
        List<Order> GetOrders
        {
            get;
        }
    }
    public delegate void TravelersChangedSubscriber();
    class TravelerCore : ITravelerCore
    {
        #region Public methods
        public TravelerCore()
        {
            m_MAS = new OdbcConnection();
            m_orders = new List<Order>();
            
            TravelersChanged = delegate { };
            m_travelers = new List<Traveler>();
            // set up the station list
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            System.IO.StreamReader stationsFile = new System.IO.StreamReader(System.IO.Path.Combine(exeDir, "stations.txt"));
            string line;
            for  ( int i = 0;  (line = stationsFile.ReadLine()) != null && line != ""; i++)
            {
                Traveler.Stations.Add(line, i);
            }
           
            // initalize the specific traveler managers
            InitializeManagers();
        }
        ~TravelerCore()
        {
            // close the MAS connection on exit
            //m_MAS.Close();
        }
        public void CreateTravelers()
        {
            // open the MAS connection
            ConnectToData();

            // Import stored orders from json file
            m_orders.Clear();
            ImportStoredOrders();

            // Import new orders from MAS
            List<Order> newOrders = new List<Order>();
            ImportOrders(ref newOrders); // also updates information on stored orders

            // Import stored travelers from json file
            m_travelers.Clear();
            ImportStoredTravelers();



            // Create and combine new travelers w/all travelers
            //m_tableManager.CompileTravelers(ref newOrders);
            //m_chairManager.CompileTravelers(ref newOrders);
            CompileTravelers(ref newOrders);

            
            // The order list has been updated
            m_orders.AddRange(newOrders);
            BackupOrders();
            // compensate order items for inventory balances
            CheckInventory();

            // Finalize the travelers by importing external information
            m_tableManager.FinalizeTravelers();
            m_chairManager.FinalizeTravelers();

            // The traveler list has been updated
            OnTravelersChanged();

            // No more data is need at this time
            CloseMAS();
        }
        public void BackupTravelers()
        {
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string contents = "";
            foreach (Traveler traveler in m_travelers)
            {
                contents += traveler.Export();

            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(exeDir, "travelers.json"),contents);
        }
        public void BackupOrders()
        {
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string contents = "";
            foreach (Order order in m_orders)
            {
                contents += order.Export();

            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(exeDir, "orders.json"), contents);
        }
        public void HandleTravelersChanged()
        {
            OnTravelersChanged();
        }
        #endregion
        //----------------------------------
        #region ITravelerCore Interface
        // Creates a child of the parent, returns the child if the quantity was not 0
        public void CreateScrapChild(Traveler parent, int qtyScrapped)
        {
            Traveler scrapped = parent.Clone();
            scrapped.Quantity = qtyScrapped;
            parent.Quantity -= qtyScrapped;
            scrapped.Start();
            scrapped.History.Add(new Event(TravelerEvent.Scrapped, scrapped.Quantity, parent.Station));
            m_travelers.Add(scrapped);

            // compensate for inventory
            UpdateQuantity(parent);
        }
        public Traveler CreateCompletedChild(Traveler parent, int qtyMade, double time)
        {
            Traveler made = (Traveler)parent.Clone();

            made.Quantity = qtyMade;
            parent.Quantity -= qtyMade;
            made.NextStation = parent.NextStation;
            made.History.Add(new Event(TravelerEvent.Completed, made.Quantity, parent.Station, time));
            m_travelers.Add(made);
            AdvanceTraveler(made);
            

            return made;
        }
       
        public Order FindOrder(string orderNo)
        {
            return m_orders.Find(x => x.SalesOrderNo == orderNo);
        }
        public Traveler FindTraveler(int ID)
        {
            return m_travelers.Find(x => x.ID == ID);
        }
        public void RemoveTraveler(Traveler traveler)
        {
            // Can only remove travelers that haven't started yet
            if (Traveler.GetStation("Start") == traveler.LastStation)
            {
                // remove itself from order items
                foreach (string orderNo in traveler.ParentOrders)
                {
                    FindOrder(orderNo).FindItem(traveler.ID).ChildTraveler = -1;
                }
                // remove itself from parents
                foreach (int parentID in traveler.Parents)
                {
                    FindTraveler(parentID).Children.Remove(traveler.ID);
                }
                // recursively remove children
                foreach (int childID in traveler.Children)
                {
                    RemoveTraveler(FindTraveler(childID));
                }
                // finally... remove THIS traveler
                m_travelers.Remove(traveler);
            }
        }
        public void AdvanceTraveler(Traveler traveler)
        {
            traveler.Station = traveler.NextStation;
            traveler.Advance();
            int ancestor = FindAncestor(traveler).ID;
            // check to see if this traveler can re-combine with family
            Traveler toRemove = null;
            foreach (Traveler relative in m_travelers)
            {
                // if they have a common ancestor
                if (relative.Station == traveler.Station && relative.ID != traveler.ID && (FindAncestor(relative).ID == ancestor))
                {
                    if (relative.ID < traveler.ID)
                    {
                        // the relative is older if the ID is less
                        relative.Quantity += traveler.Quantity;
                        Event e = new Event(TravelerEvent.Merged, traveler.Quantity, traveler.LastStation);
                        e.message = "Traveler [" + traveler.ID.ToString("D6") + "] has merged with this traveler. Please combine it's parts with this traveler's parts and destroy it's label.";
                        relative.History.Add(e);
                        toRemove = traveler;
                    } else
                    {
                        // the traveler is older than the relative
                        traveler.Quantity += relative.Quantity;
                        Event e = new Event(TravelerEvent.Merged, relative.Quantity, relative.LastStation);
                        e.message = "Traveler [" + relative.ID.ToString("D6") + "] has merged with this traveler. Please combine its parts with this traveler's parts and destroy its label.";
                        traveler.History.Add(e);
                        toRemove = relative;
                    }
                }
            }
            if (toRemove != null)
            {
                RemoveTraveler(toRemove);
            }
        }
        public Traveler FindAncestor(Traveler child)
        {
            foreach (int parentID in child.Parents)
            {
                return FindAncestor(FindTraveler(parentID));
            }
            return child;
        }
        public List<Traveler> GetTravelers
        {
            get
            {
                return m_travelers;
            }
        }
        public List<Order> GetOrders
        {
            get
            {
                return m_orders;
            }
        }
        #endregion
        //----------------------------------
        #region Private methods
        // Gets the total quantity ordered, compensated by what is in stock
        private int QuantityNeeded(Traveler traveler)
        {
            int qtyNeeded = 0;
            foreach (string orderNo in traveler.ParentOrders)
            {
                Order order = FindOrder(orderNo);
                if (order != null)
                {
                    foreach (OrderItem item in order.Items)
                    {
                        if (item.ChildTraveler == traveler.ID)
                        {
                            qtyNeeded += Math.Max(0, item.QtyOrdered - item.QtyOnHand);
                        }
                    }
                }
            }
            return qtyNeeded;
        }
        // updates the quantity of a traveler and all its children
        private void UpdateQuantity(Traveler traveler)
        {
            // 1.) compensate highest level traveler with inventory
            // if it has parent orders and hasn't started, the quantity can change
            if (traveler.LastStation == Traveler.GetStation("Start") && traveler.ParentOrders.Count > 0)
            {
                traveler.Quantity = QuantityNeeded(traveler);
            }
            // 2.) adjust children quantities
            if (traveler.Children.Count > 0)
            {
                int qtyNeeded = Math.Max(0, QuantityNeeded(traveler) - traveler.Quantity);
                List<Traveler> started = new List<Traveler>();
                List<Traveler> notStarted = new List<Traveler>();
                foreach (int childID in traveler.Children)
                {
                    Traveler child = FindTraveler(childID);
                    if (child != null)
                    {
                        // update children of child
                        // can only change quantity if this child hasn't started
                        if (child.LastStation == Traveler.GetStation("Start"))
                        {
                            notStarted.Add(child);
                        }
                        else
                        {
                            started.Add(child);
                            qtyNeeded -= child.Quantity;
                        }
                    }
                }
                foreach (Traveler child in notStarted)
                {
                    if (qtyNeeded == 0)
                    {
                        m_travelers.Remove(child); // don't need this anymore
                        traveler.Children.RemoveAll(x => x == child.ID);
                    }
                    else
                    {
                        child.Quantity = qtyNeeded;
                        qtyNeeded = 0;
                    }
                }
            }
        }
        // reserve inventory items under order items by item type (by traveler)
        private void CheckInventory()
        {
            try
            {
                foreach (Traveler traveler in m_travelers)
                {
                    if (m_MAS.State != System.Data.ConnectionState.Open) throw new Exception("MAS is in a closed state!");
                    OdbcCommand command = m_MAS.CreateCommand();
                    command.CommandText = "SELECT QuantityOnSalesOrder, QuantityOnHand FROM IM_ItemWarehouse WHERE ItemCode = '" + traveler.ItemCode + "'";
                    OdbcDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        int onHand = Convert.ToInt32(reader.GetValue(1));
                        // adjust the quantity on hand for orders
                        List<Order> parentOrders = new List<Order>();
                        foreach (string orderNo in traveler.ParentOrders)
                        {
                            parentOrders.Add(FindOrder(orderNo));
                        }
                        parentOrders.Sort((a, b) => b.OrderDate.CompareTo(a.OrderDate)); // sort in descending order (oldest first)
                        for (int i = 0; i < parentOrders.Count && onHand > 0; i++)
                        {
                            Order order = parentOrders[i];
                            foreach (OrderItem item in order.Items)
                            {
                                if (item.ChildTraveler == traveler.ID)
                                {
                                    item.QtyOnHand = Math.Min(onHand, item.QtyOrdered);
                                    onHand -= item.QtyOnHand;
                                }
                            }
                        }
                    }
                    reader.Close();
                }
            } catch (Exception ex)
            {
                Server.WriteLine("Problem checking order items against inventory: " + ex.Message + " Stack Trace: " + ex.StackTrace);
            }
        }
        // Opens a connection to the MAS database
        private void ConnectToData()
        {
            Server.WriteLine("Connecting to MAS");
            
            // initialize the MAS connection
            m_MAS.ConnectionString = "DSN=SOTAMAS90;Company=MGI;";
            m_MAS.ConnectionString = "DSN=SOTAMAS90;Company=MGI;UID=GKC;PWD=sgp4x347;";
            try
            {
                m_MAS.Open();
            }
            catch (Exception ex)
            {
                Server.WriteLine("Failed to log in :" + ex.Message);
            }
        }
        private void CloseMAS()
        {
            m_MAS.Close();
            Server.WriteLine("Disconnected from MAS");
        }
        private void InitializeManagers()
        {
            m_tableManager = new TableManager(ref m_MAS,this as ITravelerCore);
            m_chairManager = new ChairManager(ref m_MAS,this as ITravelerCore);
        }

        // Imports and stores all open orders that have not already been stored
        private void ImportOrders(ref List<Order> orders)
        {
            try
            {
                Server.WriteLine("Importing orders...");
                List<string> currentOrderNumbers = new List<string>();
                // get informatino from header
                if (m_MAS.State != System.Data.ConnectionState.Open) throw new Exception("MAS is in a closed state!");
                OdbcCommand command = m_MAS.CreateCommand();
                command.CommandText = "SELECT SalesOrderNo, CustomerNo, ShipVia, OrderDate, ShipExpireDate FROM SO_SalesOrderHeader";
                OdbcDataReader reader = command.ExecuteReader();
                // read info
                int max = 20;
                while (reader.Read() && max > 0)
                {
                    max--;
                    string salesOrderNo = reader.GetString(0);
                    currentOrderNumbers.Add(salesOrderNo);
                    int index = m_orders.FindIndex(x => x.SalesOrderNo == salesOrderNo);

                    // does not match any stored records
                    if (index == -1)
                    {
                        // create a new order
                        Order order = new Order();
                        if (!reader.IsDBNull(0)) order.SalesOrderNo = reader.GetString(0);
                        if (!reader.IsDBNull(1)) order.CustomerNo = reader.GetString(1);
                        if (!reader.IsDBNull(2)) order.ShipVia = reader.GetString(2);
                        if (order.ShipVia == null) order.ShipVia = ""; // havent found a shipper yet, will be LTL regardless
                        if (!reader.IsDBNull(3)) order.OrderDate = reader.GetDateTime(3);
                        if (!reader.IsDBNull(4)) order.ShipDate = reader.GetDateTime(4);
                        // get information from detail
                        if (m_MAS.State != System.Data.ConnectionState.Open) throw new Exception("MAS is in a closed state!");
                        OdbcCommand detailCommand = m_MAS.CreateCommand();
                        detailCommand.CommandText = "SELECT ItemCode, QuantityOrdered, UnitOfMeasure FROM SO_SalesOrderDetail WHERE SalesOrderNo = '" + reader.GetString(0) + "'";
                        OdbcDataReader detailReader = detailCommand.ExecuteReader();

                        // Read each line of the Sales Order, looking for the base Table, Chair, ect items, ignoring kits
                        while (detailReader.Read())
                        {
                            string billCode = detailReader.GetString(0);
                            if (!detailReader.IsDBNull(2) && detailReader.GetString(2) != "KIT")
                            {
                                OrderItem item = new OrderItem();
                                if (!detailReader.IsDBNull(0)) item.ItemCode = detailReader.GetString(0);  // itemCode
                                if (!detailReader.IsDBNull(1)) item.QtyOrdered = Convert.ToInt32(detailReader.GetValue(1)); // Quantity
                                order.Items.Add(item);
                            }
                        }
                        detailReader.Close();
                        orders.Add(order);
                    }
                    // Update information for existing order
                    else
                    {
                        if (!reader.IsDBNull(1)) m_orders[index].CustomerNo = reader.GetString(1);
                        if (!reader.IsDBNull(2)) m_orders[index].ShipVia = reader.GetString(2);
                        if (m_orders[index].ShipVia == null) m_orders[index].ShipVia = ""; // havent found a shipper yet, will be LTL regardless
                        if (!reader.IsDBNull(3)) m_orders[index].OrderDate = reader.GetDateTime(3);
                        if (!reader.IsDBNull(4)) m_orders[index].ShipDate = reader.GetDateTime(4);
                    }
                }
                reader.Close();
                // cull orders that do not exist anymore
                List<Order> preCullList = new List<Order>(m_orders);
                m_orders.Clear();
                foreach (Order order in preCullList)
                {
                    if (currentOrderNumbers.Exists(x => x == order.SalesOrderNo))
                    {
                        // phew! the order is still here
                        m_orders.Add(order);
                    }
                }
            }
            catch (Exception ex)
            {
                Server.WriteLine("Problem importing new orders: " + ex.Message + " Stack Trace: " + ex.StackTrace);
            }
        }
        // Imports orders that have been stored
        private void ImportStoredOrders()
        {
            // create the file if it doesn't exist
            StreamWriter w = File.AppendText("orders.json");
            w.Close();
            // open the file
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string line;
            System.IO.StreamReader file = new System.IO.StreamReader(System.IO.Path.Combine(exeDir, "orders.json"));
            while ((line = file.ReadLine()) != null && line != "")
            {
                m_orders.Add(new Order(line));
            }
            file.Close();
        }
        // Imports travelers that have been stored
        private void ImportStoredTravelers()
        {
            //--------------------------------------------------------------
            // get the list of travelers and orders that have been created
            //--------------------------------------------------------------
            // create the file if it doesn't exist
            StreamWriter w = File.AppendText("travelers.json");
            w.Close();
            // open the file
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string line;
            System.IO.StreamReader file = new System.IO.StreamReader(System.IO.Path.Combine(exeDir, "travelers.json"));
            double travelerCount = File.ReadLines(System.IO.Path.Combine(exeDir, "travelers.json")).Count();
            int index = 0;
            while ((line = file.ReadLine()) != null && line != "")
            {
                Server.Write("\r{0}%", "Loading travelers from backup..." + Convert.ToInt32((Convert.ToDouble(index) / travelerCount) * 100));
                StringStream ss = new StringStream(line);
                Dictionary<string, string> obj = ss.ParseJSON();

                // check to see if these orders have been printed already
                Traveler traveler = new Traveler(obj);
                // cull orders that do not exist anymore
                List<string> preCullList = new List<string>(traveler.ParentOrders);
                traveler.ParentOrders.Clear();
                foreach (string orderNo in preCullList)
                {
                    if (m_orders.Exists(x => x.SalesOrderNo == orderNo))
                    {
                        // Phew! that order is still kicking
                        traveler.ParentOrders.Add(orderNo);
                    }
                }
                // if there are no more orders around, this traveler hits the dumpster
                if (traveler.ParentOrders.Count > 0)
                {
                    // import type-specific information
                    switch (obj["type"])
                    {
                        case "Table":
                            Table table = new Table(traveler,true);
                            // Relational -------------------------------
                            table.ParentOrders = traveler.ParentOrders;
                            table.Parents = traveler.Parents;
                            table.Children = traveler.Children;
                            //-------------------------------------------
                            table.ImportPart(ref m_MAS);
                            if (table.Station == Traveler.GetStation("Start")) table.Start();
                            table.Advance();
                            m_tableManager.FinalizeTable(table);
                            m_travelers.Add(table);
                            break;
                        case "Chair":
                            Chair chair = new Chair(traveler,true);
                            // Relational -------------------------------
                            chair.ParentOrders = chair.ParentOrders;
                            chair.Parents = traveler.Parents;
                            chair.Children = traveler.Children;
                            //-------------------------------------------
                            chair.ImportPart(ref m_MAS);
                            if (chair.Station == Traveler.GetStation("Start")) chair.Start();
                            chair.Advance();
                            m_travelers.Add(chair);
                            break;
                    }
                    
                }
                index++;
            }
            Server.Write("\r{0}", "Loading travelers from backup...Finished\n");

            file.Close();
        }
        private bool IsBackPanel(string s)
        {
            if (s.Substring(0, 2) == "32")
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        private void OnTravelersChanged()
        {
            // Update the travelers.json file with all the current travelers
            BackupTravelers();
            // fire the event
            TravelersChanged();
        }
        public virtual void CompileTravelers(ref List<Order> newOrders)
        {
            int index = 0;
            foreach (Order order in newOrders)
            {
                foreach (OrderItem item in order.Items)
                {
                    // only make a traveler if this one has no child traveler already (-1 signifies no child traveler)
                    if (item.ChildTraveler < 0 && (Traveler.IsTable(item.ItemCode) || Traveler.IsChair(item.ItemCode)))
                    {
                        Console.Write("\r{0}%   ", "Compiling Travelers..." + Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(newOrders.Count)) * 100));

                        // search for existing traveler
                        // can only combine if same itemCode, hasn't started, and has no parents
                        Traveler traveler = m_travelers.Find(x => x.ItemCode == item.ItemCode && x.LastStation == Traveler.GetStation("Start") && x.Parents.Count == 0);
                        if (traveler != null)
                        {
                            // add to existing traveler
                            traveler.Quantity += item.QtyOrdered;

                            // RELATIONAL =============================================================
                            item.ChildTraveler = traveler.ID;
                            traveler.ParentOrders.Add(order.SalesOrderNo);
                            //=========================================================================
                        }
                        else
                        {
                            // create a new traveler from the new item
                            Traveler newTraveler = (Traveler.IsTable(item.ItemCode) ? (Traveler)new Table(item.ItemCode,item.QtyOrdered,ref m_MAS) : (Traveler)new Chair(item.ItemCode, item.QtyOrdered, ref m_MAS));

                            // RELATIONAL =============================================================
                            item.ChildTraveler = newTraveler.ID;
                            newTraveler.ParentOrders.Add(order.SalesOrderNo);
                            //=========================================================================

                            // start the new traveler's journey
                            newTraveler.Start();
                            // add the new traveler to the list
                            m_travelers.Add(newTraveler);
                        }
                    }
                }
                index++;
            }
            Console.Write("\r{0}   ", "Compiling Tables...Finished\n");
        }

        #endregion
        //----------------------------------
        #region Private member variables

        private TableManager m_tableManager;
        private ChairManager m_chairManager;

        private List<Order> m_orders;
        public List<Traveler> m_travelers;

        public event TravelersChangedSubscriber TravelersChanged;
        private OdbcConnection m_MAS;
        #endregion
    }
}
