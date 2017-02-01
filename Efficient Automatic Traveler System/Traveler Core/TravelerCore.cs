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
using Excel = Microsoft.Office.Interop.Excel;
using Marshal = System.Runtime.InteropServices.Marshal;

namespace Efficient_Automatic_Traveler_System
{
    // Class: Used to generate and store the digital "travelers" that are used throughout the system
    // Developer: Gage Coates
    // Date started: 1/25/16
    public delegate void TravelersChangedEvent();
    class TravelerCore
    {
        //------------------------------
        // Public members
        //------------------------------
        public TravelerCore()
        {
            m_orders = new List<Order>();
            m_excelApp = new Excel.Application();
            m_excelApp.DisplayAlerts = false;
            m_workbooks = m_excelApp.Workbooks;
            TravelersChanged = delegate { };
            Travelers = new List<Traveler>();

            InitializeManagers();
        }
        ~TravelerCore()
        {
            // close the MAS connection on exit
            m_MAS.Close();
            // close excel

            m_workbooks.Close();
            if (m_workbooks != null) Marshal.FinalReleaseComObject(m_workbooks);
            m_excelApp.Quit();
            if (m_excelApp != null) Marshal.FinalReleaseComObject(m_excelApp);
        }
        public void CreateTravelers()
        {
            m_tableManager.Reset();
            m_chairManager.Reset();
            List<Traveler> newTravelers = new List<Traveler>();
            // Import stored travelers
            ImportStored();

            // Import new orders
            ImportOrders();

            // Create Tables
            m_tableManager.CompileTravelers();
            newTravelers.AddRange(m_tableManager.Travelers);
            // Create Chairs
            m_chairManager.CompileTravelers();
            newTravelers.AddRange(m_chairManager.Travelers);

            // The traveler list has been updated
            Travelers.Clear();

            Travelers.AddRange(newTravelers);
            OnTravelersChanged();
            // Update the travelers.json file with all the current travelers
            BackupTravelers();
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
        public List<Traveler> GetTravelersAt(ProductionStage stage)
        {
            List<Traveler> travelers = new List<Traveler>();
            foreach (Traveler traveler in Travelers)
            {
                if (traveler.ProductionStage == stage)
                {
                    travelers.Add(traveler);
                }
            }
            return travelers;
        }
        //------------------------------
        // Private members
        //------------------------------

        // Opens a connection to the MAS database
        private void ConnectToData()
        {
            Console.WriteLine("Logging into MAS");
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
                Console.WriteLine("Failed to log in :" + ex.Message);
            }
        }
        private void InitializeManagers()
        {
            ConnectToData();

            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var workbook = m_workbooks.Open(System.IO.Path.Combine(exeDir, "Kanban Blank Color Cross Reference.xlsx"),
                0, false, 5, "", "", false, 2, "",
                true, false, 0, true, false, false);
            //var workbook = workbooks.Open(@"\\Mgfs01\share\common\Quick Ship Traveler\Kanban Blank Color Cross Reference.xlsx",
            //    0, false, 5, "", "", false, 2, "",
            //    true, false, 0, true, false, false);
            var worksheets = workbook.Worksheets;
            var crossRef = (Excel.Worksheet)worksheets.get_Item("Blank Cross Reference");
            var colorRef = (Excel.Worksheet)worksheets.get_Item("Color Families");
            var blankRef = (Excel.Worksheet)worksheets.get_Item("Blank Parent");
            var boxRef = (Excel.Worksheet)worksheets.get_Item("Box Size");

            m_tableManager = new TableManager(m_MAS, crossRef, boxRef, blankRef, colorRef);
            m_chairManager = new ChairManager(m_MAS);
        }
        
        private void Clear()
        {
            m_tableManager.Orders.Clear();
            m_tableManager.Travelers.Clear();
            m_chairManager.Orders.Clear();
            m_chairManager.Travelers.Clear();
        }

        // Imports and stores all open orders that have not already been stored
        private void ImportOrders()
        {
            Console.WriteLine("Importing orders...");
            string today = DateTime.Today.ToString(@"yyyy\-MM\-dd");

            
            // get informatino from header
            OdbcCommand command = m_MAS.CreateCommand();
            command.CommandText = "SELECT SalesOrderNo, CustomerNo, ShipVia, ShipExpireDate FROM SO_SalesOrderHeader";
            OdbcDataReader reader = command.ExecuteReader();
            // read info
            while (reader.Read())
            {
                string salesOrderNo = reader.GetString(0);
                // continue to the next order if this order already has a traveler
                if (m_orders.Exists(x => x.SalesOrderNo == salesOrderNo))
                {
                    continue;
                }
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
                        if (Traveler.IsTable(billCode))
                        {
                            // this is a table
                            Order order = new Order();
                            // scrap this order if anything is missing
                            if (!reader.IsDBNull(0))
                            {
                                order.SalesOrderNo = salesOrderNo;
                            }
                            if (!reader.IsDBNull(1))
                            {
                                order.CustomerNo = reader.GetString(1);
                            }
                            if (!reader.IsDBNull(2))
                            {
                                order.ShipVia = reader.GetString(2);
                            }
                            if (!reader.IsDBNull(3))
                            {
                                order.OrderDate = reader.GetDate(3);
                            }

                            order.ItemCode = billCode;
                            order.QuantityOrdered = Convert.ToInt32(detailReader.GetValue(1));
                            m_tableManager.Orders.Add(order);
                        }
                        else if (Traveler.IsChair(billCode))
                        {
                            // this is a table
                            Order order = new Order();
                            // scrap this order if anything is missing
                            if (!reader.IsDBNull(0))
                            {
                                order.SalesOrderNo = salesOrderNo;
                            }
                            if (!reader.IsDBNull(1))
                            {
                                order.CustomerNo = reader.GetString(1);
                            }
                            if (!reader.IsDBNull(2))
                            {
                                order.ShipVia = reader.GetString(2);
                            }
                            if (!reader.IsDBNull(3))
                            {
                                order.OrderDate = reader.GetDate(3);
                            }

                            order.ItemCode = billCode;
                            order.QuantityOrdered = Convert.ToInt32(detailReader.GetValue(1));
                            m_chairManager.Orders.Add(order);
                        }
                        else if (IsBackPanel(billCode))
                        {
                            // this is probably a back panel for an apex standup desk
                            Order order = new Order();
                            // scrap this order if anything is missing
                            if (!reader.IsDBNull(0))
                            {
                                order.SalesOrderNo = salesOrderNo;
                            }
                            if (!reader.IsDBNull(1))
                            {
                                order.CustomerNo = reader.GetString(1);
                            }
                            if (!reader.IsDBNull(2))
                            {
                                order.ShipVia = reader.GetString(2);
                            }
                            if (!reader.IsDBNull(3))
                            {
                                order.OrderDate = reader.GetDate(3);
                            }
                            order.ItemCode = billCode;
                            order.QuantityOrdered = Convert.ToInt32(detailReader.GetValue(1));
                        }
                    }
                }
                detailReader.Close();
            }
            reader.Close();
        }
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
                Console.Write("\r{0}%   ", "Loading travelers from backup..." + Convert.ToInt32((Convert.ToDouble(index) / travelerCount) * 100));
                Traveler createdTraveler = new Traveler(line);
                m_orders.AddRange(createdTraveler.Orders);
                // check to see if these orders have been printed already
                if (Traveler.IsTable(createdTraveler.PartNo))
                {
                    Table table = new Table(line);
                    table.ImportPart(m_MAS);
                    m_tableManager.Travelers.Add(table);
                } else if (Traveler.IsChair(createdTraveler.PartNo))
                {
                    Chair chair = new Chair(line);
                    chair.ImportPart(m_MAS);
                    m_chairManager.Travelers.Add(chair);
                }
                index++;
            }
            Console.Write("\r{0}   ", "Loading travelers from backup...Finished\n");

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
            TravelersChanged();
        }

        //------------------------------
        // Properties
        //------------------------------
        private List<Order> m_orders;
        private TableManager m_tableManager;
        private ChairManager m_chairManager;

        public List<Traveler> m_travelers;
        public event TravelersChangedEvent TravelersChanged;
        //private List<Traveler> m_weeke;
        //private List<Traveler> m_heian;
        //private List<Traveler> m_vector;
        //private List<Traveler> m_box;
        //private List<Traveler> m_assm;

        private Excel.Application m_excelApp;
        private Excel.Workbooks m_workbooks;
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
