﻿using System;
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
            m_MAS = new OdbcConnection();
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
            m_tableManager.CompileTravelers(ref newOrders);
            m_chairManager.CompileTravelers(ref newOrders);

            
            // The order list has been updated
            m_orders.AddRange(newOrders);
            BackupOrders();
            // compensate order items for inventory balances
            CheckInventory();

            // Finalize the travelers by importing external information
            m_tableManager.ImportInformation();
            m_chairManager.ImportInformation();

            // The traveler list has been updated
            OnTravelersChanged();

            // No more data is need at this time
            CloseMAS();
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

        // reserve inventory items under order items by item type (by traveler)
        private void CheckInventory()
        {
            
            foreach (Traveler traveler in m_travelers)
            {
                OdbcCommand command = m_MAS.CreateCommand();
                command.CommandText = "SELECT QuantityOnSalesOrder, QuantityOnHand FROM IM_ItemWarehouse WHERE ItemCode = '" + traveler.PartNo + "'";
                OdbcDataReader reader = command.ExecuteReader();
                if (reader.Read())
                {
                    int onHand = Convert.ToInt32(reader.GetValue(1));
                    // adjust the quantity on hand for orders
                    List<Order> parentOrders = new List<Order>();
                    foreach (string orderNo in traveler.ParentOrders)
                    {
                        parentOrders.Add(m_orders[FindOrderIndex(orderNo)]);
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
            m_tableManager = new TableManager(ref m_MAS,ref m_orders, ref m_travelers);
            m_chairManager = new ChairManager(ref m_MAS,ref m_orders, ref m_travelers);
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
            List<string> currentOrderNumbers = new List<string>();
            // get informatino from header
            OdbcCommand command = m_MAS.CreateCommand();
            command.CommandText = "SELECT SalesOrderNo, CustomerNo, ShipVia, OrderDate, ShipExpireDate FROM SO_SalesOrderHeader";
            OdbcDataReader reader = command.ExecuteReader();
            // read info
            while (reader.Read())
            {
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
                            Table table = new Table(traveler);
                            table.ImportPart(ref m_MAS);
                            if (table.Station == Traveler.GetStation("Start")) table.Start();
                            table.Advance();
                            m_tableManager.ImportInformation(table);
                            m_travelers.Add(table);
                            break;
                        case "Chair":
                            Chair chair = new Chair(traveler);
                            chair.ImportPart(ref m_MAS);
                            if (chair.Station == Traveler.GetStation("Start")) chair.Start();
                            chair.Advance();
                            m_travelers.Add(chair);
                            break;
                    }
                    
                } else
                {
                    var catchme = true;
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

        internal List<Order> Orders
        {
            get
            {
                return m_orders;
            }

            set
            {
                m_orders = value;
            }
        }
    }
}
