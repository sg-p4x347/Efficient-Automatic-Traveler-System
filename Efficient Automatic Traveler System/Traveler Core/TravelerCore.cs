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

            m_travelers = new List<Traveler>();

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
            // Import stored travelers
            ImportStored();

            // Import new orders
            //ImportOrders();
            //// Create Tables
            //m_tableManager.CompileTravelers();
            //m_travelers.AddRange(m_tableManager.Travelers);
            //// Create Chairs
            //m_chairManager.CompileTravelers();
            //m_travelers.AddRange(m_chairManager.Travelers);

            //BackupTravelers();
        }
        public void BackupTravelers()
        {
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            System.IO.StreamWriter file = File.AppendText(System.IO.Path.Combine(exeDir, "travelers.json"));
            foreach (Traveler traveler in m_travelers)
            {
                file.Write(traveler.Export());
            }
            file.Close();
        }
        public List<Traveler> GetTravelersAt(ProductionStage stage)
        {
            List<Traveler> travelers = new List<Traveler>();
            foreach (Traveler traveler in m_travelers)
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
                        if (IsTable(billCode))
                        {
                            // this is a table
                            Order order = new Order();
                            // scrap this order if anything is missing
                            if (!reader.IsDBNull(0))
                            {
                                order.SalesOrderNo = reader.GetString(0);
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
                        else if (IsChair(billCode))
                        {
                            // this is a table
                            Order order = new Order();
                            // scrap this order if anything is missing
                            if (!reader.IsDBNull(0))
                            {
                                order.SalesOrderNo = reader.GetString(0);
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
                                order.SalesOrderNo = reader.GetString(0);
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
            //==========================================
            //get the list of travelers that have been created
            //==========================================
            List<Traveler> createdTravelers = new List<Traveler>();
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string line;
            System.IO.StreamReader file = new System.IO.StreamReader(System.IO.Path.Combine(exeDir, "travelers.json"));
            while ((line = file.ReadLine()) != null && line != "")
            {
                Traveler createdTraveler = new Traveler(line);

                // check to see if these orders have been printed already
                if (IsTable(createdTraveler.PartNo))
                {
                    Table table = new Table(line);
                    table.ImportPart(m_MAS);
                    createdTravelers.Add(table);
                } else if (IsChair(createdTraveler.PartNo))
                {
                    Chair chair = new Chair(line);
                    chair.ImportPart(m_MAS);
                    createdTravelers.Add(chair);
                }
            }
            file.Close();
            m_travelers.AddRange(createdTravelers);
        }
        private bool IsTable(string s)
        {
            return (s.Length == 9 && s.Substring(0, 2) == "MG") || (s.Length == 10 && (s.Substring(0, 3) == "38-" || s.Substring(0, 3) == "41-"));
        }
        private bool IsChair(string s)
        {
            if (s.Length == 14 && s.Substring(0, 2) == "38")
            {
                string[] parts = s.Split('-');
                return (parts[0].Length == 5 && parts[1].Length == 4 && parts[2].Length == 3);
            }
            else if (s.Length == 15 && s.Substring(0, 4) == "MG11")
            {
                string[] parts = s.Split('-');
                return (parts[0].Length == 6 && parts[1].Length == 4 && parts[2].Length == 3);
            }
            else
            {
                return false;
            }

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

        //------------------------------
        // Properties
        //------------------------------
        private List<Order> m_orders;
        private TableManager m_tableManager;
        private ChairManager m_chairManager;

        private List<Traveler> m_travelers;
        //private List<Traveler> m_weeke;
        //private List<Traveler> m_heian;
        //private List<Traveler> m_vector;
        //private List<Traveler> m_box;
        //private List<Traveler> m_assm;

        private Excel.Application m_excelApp;
        private Excel.Workbooks m_workbooks;
        private OdbcConnection m_MAS;
    }
}
