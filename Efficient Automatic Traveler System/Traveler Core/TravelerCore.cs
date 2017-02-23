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
    public delegate void TravelersChangedSubscriber();
    class TravelerCore
    {
        //------------------------------
        // Public members
        //------------------------------
        public TravelerCore()
        {
            m_orders = new List<Order>();
            
            TravelersChanged = delegate { };
            Travelers = new List<Traveler>();
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
            m_tableManager.Reset();
            m_chairManager.Reset();
            
            // Import stored travelers
            ImportStored();

            // Import stored orders
            ImportStoredOrders();

            // Import new orders
            List<Order> newOrders = new List<Order>();
            ImportOrders(ref newOrders);
            BackupOrders();

            List<Traveler> newTravelers = new List<Traveler>();
            // Create and combine new Table travelers
            m_tableManager.CompileTravelers(ref newOrders);
            newTravelers.AddRange(m_tableManager.Travelers);
            // Create and combine new Chair travelers
            m_chairManager.CompileTravelers(ref newOrders);
            newTravelers.AddRange(m_chairManager.Travelers);

            // The traveler list has been updated
            Travelers.Clear();
            Travelers.AddRange(newTravelers);

            // The order list has been updated
            m_orders.Clear();
            m_orders.AddRange(newOrders);

            // Finalize the travelers by importing external information
            m_tableManager.ImportInformation();
            m_chairManager.ImportInformation();

            OnTravelersChanged();
        }
        public void BackupTravelers()
        {
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string contents = "";
            foreach (Traveler traveler in Travelers)
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
        // Relational
        public int FindOrderIndex(string orderNo)
        {
            for (int index = 0; index < m_orders.Count; index++)
            {
                if (m_orders[index].SalesOrderNo == orderNo) return index;
            }
            return -1;
        }

        //------------------------------
        // Private members
        //------------------------------

        // Opens a connection to the MAS database
        private void ConnectToData()
        {
            Server.WriteLine("Logging into MAS");
            m_MAS = new OdbcConnection();
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
        private void InitializeManagers()
        {
            ConnectToData();

            m_tableManager = new TableManager(m_MAS,ref m_orders);
            m_chairManager = new ChairManager(m_MAS,ref m_orders);
        }
        
        private void Clear()
        {
            m_tableManager.Orders.Clear();
            m_tableManager.Travelers.Clear();
            m_chairManager.Orders.Clear();
            m_chairManager.Travelers.Clear();
        }

        // Imports and stores all open orders that have not already been stored
        private void ImportOrders(ref List<Order> orders)
        {
            Server.WriteLine("Importing orders...");
            
            // get informatino from header
            OdbcCommand command = m_MAS.CreateCommand();
            command.CommandText = "SELECT SalesOrderNo, CustomerNo, ShipVia, OrderDate, ShipExpireDate FROM SO_SalesOrderHeader";
            OdbcDataReader reader = command.ExecuteReader();
            // read info
            while (reader.Read())
            {
                string salesOrderNo = reader.GetString(0);
                int index = m_orders.FindIndex(x => x.SalesOrderNo == salesOrderNo);
                
                // does not match any stored records
                if (index == -1)
                {
                    // create a new order
                    Order order = new Order();
                    if (!reader.IsDBNull(0)) order.SalesOrderNo = reader.GetString(0);
                    if (!reader.IsDBNull(1)) order.CustomerNo = reader.GetString(1);
                    if (!reader.IsDBNull(2)) order.ShipVia = reader.GetString(2);
                    if (!reader.IsDBNull(3)) order.OrderDate = reader.GetDateTime(3);
                    if (!reader.IsDBNull(4)) order.ShipDate =reader.GetDateTime(4);
                    // get information from detail
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
                    if (!reader.IsDBNull(3)) m_orders[index].OrderDate = reader.GetDateTime(3);
                    if (!reader.IsDBNull(4)) m_orders[index].ShipDate = reader.GetDateTime(4);
                }
            }
            reader.Close();
        }
        // Imports orders that have been stored
        private void ImportStoredOrders()
        {
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
        private void ImportStored()
        {
            //--------------------------------------------------------------
            // get the list of travelers and orders that have been created
            //--------------------------------------------------------------
            m_orders.Clear();
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string line;
            System.IO.StreamReader file = new System.IO.StreamReader(System.IO.Path.Combine(exeDir, "travelers.json"));
            double travelerCount = File.ReadLines(System.IO.Path.Combine(exeDir, "travelers.json")).Count();
            int index = 0;
            while ((line = file.ReadLine()) != null && line != "")
            {
                Server.Write("\r{0}%   ", "Loading travelers from backup..." + Convert.ToInt32((Convert.ToDouble(index) / travelerCount) * 100));
                StringStream ss = new StringStream(line);
                Dictionary<string, string> obj = ss.ParseJSON();

                // check to see if these orders have been printed already
                switch (obj["type"])
                {
                    case "Table":
                        Table table = new Table(obj);
                        table.ImportPart(m_MAS);
                        if (table.Station == Traveler.GetStation("Start")) table.Start();
                        table.Advance();
                        m_tableManager.Travelers.Add(table);
                        break;
                    case "Chair":
                        Chair chair = new Chair(obj);
                        chair.ImportPart(m_MAS);
                        if (chair.Station == Traveler.GetStation("Start")) chair.Start();
                        chair.Advance();
                        m_chairManager.Travelers.Add(chair);
                        break;
                }
                index++;
            }
            Server.Write("\r{0}   ", "Loading travelers from backup...Finished\n");

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

        //------------------------------
        // Properties
        //------------------------------
        private List<Order> m_orders;
        private TableManager m_tableManager;
        private ChairManager m_chairManager;

        public List<Traveler> m_travelers;
        public event TravelersChangedSubscriber TravelersChanged;
        //private List<Traveler> m_weeke;
        //private List<Traveler> m_heian;
        //private List<Traveler> m_vector;
        //private List<Traveler> m_box;
        //private List<Traveler> m_assm;
        private OdbcConnection m_MAS;

        internal List<Traveler> Travelers
        {
            get
            {
                return m_travelers;
            }

            set
            {
                m_travelers = value;
                TravelersChanged();
            }
        }
    }
}
