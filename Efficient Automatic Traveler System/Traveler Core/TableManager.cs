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
using System.Net.Http;

namespace Efficient_Automatic_Traveler_System
{
    class TableManager
    {
        //-----------------------
        // Public members
        //-----------------------
        public TableManager() { }
        public TableManager(OdbcConnection mas, Excel.Worksheet crossRef, Excel.Worksheet boxRef, Excel.Worksheet blankRef, Excel.Worksheet colorRef)
        {
            m_MAS = mas;
            m_crossRef = crossRef;
            m_boxRef = boxRef;
            m_blankRef = blankRef;
            m_colorRef = colorRef;
        }
        
        public void CompileTravelers()
        {
            Console.WriteLine("");
            
            // clear any previous travelers
            m_travelers.Clear();

            //==========================================
            // compile the travelers
            //==========================================
                
            int index = 0;
            foreach (Order order in m_orders)
            {
                
                Console.Write("\r{0}%   ", "Compiling Tables..." + Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(m_orders.Count)) * 100));
                // Make a unique traveler for each order, while combining common parts from different models into single traveler
                bool foundBill = false;
                // search for existing traveler
                foreach (Table traveler in m_travelers)
                {
                    if (traveler.Part == null) traveler.ImportPart(MAS);
                    if (traveler.Part.BillNo == order.ItemCode)
                    {
                        // update existing traveler
                        foundBill = true;
                        // add to the quantity of items
                        traveler.Quantity += order.QuantityOrdered;
                        // add to the order list
                        traveler.Orders.Add(order);
                    }
                }
                if (!foundBill)
                {
                    // create a new traveler from the new item
                    Table newTraveler = new Table(order.ItemCode, order.QuantityOrdered, MAS);
                    // add to the order list
                    newTraveler.Orders.Add(order);
                    // add the new traveler to the list
                    m_travelers.Add(newTraveler);
                }
                index++;
            }
            Console.Write("\r{0}   ", "Compiling Tables...Finished");
            ImportInformation();
        }
        //-----------------------
        // Private members
        //-----------------------
        private Traveler FindTraveler(string s)
        {
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string line;
            System.IO.StreamReader file = new System.IO.StreamReader(System.IO.Path.Combine(exeDir, "printed.json"));
            int travelerID = 0;
            try
            {
                if (s.Length < 7)
                {
                    travelerID = Convert.ToInt32(s);
                }
            }
            catch (Exception ex)
            {

            }
            while ((line = file.ReadLine()) != null && line != "")
            {
                Traveler printedTraveler = new Traveler(line);
                // check to see if the number matches a traveler ID
                if (travelerID == printedTraveler.ID)
                {
                    return printedTraveler;
                }
                // check to see if these orders have been printed already
                foreach (Order printedOrder in printedTraveler.Orders)
                {
                    if (printedOrder.SalesOrderNo == s)
                    {
                        return printedTraveler;
                    }
                }
            }
            return null;
        }
        private void ImportInformation()
        {
            Console.WriteLine("");
            int index = 0;
            foreach (Table traveler in m_travelers)
            {
                if (traveler.Part == null) traveler.ImportPart(MAS);
                Console.Write("\r{0}%   ", "Importing Table Info..." + Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(m_travelers.Count)) * 100));
                traveler.CheckInventory(MAS);
                // update and total the final parts
                traveler.Part.TotalQuantity = traveler.Quantity;
                traveler.FindComponents(traveler.Part);
                // Table specific
                GetColorInfo(traveler);
                GetBoxInfo(traveler);
                GetBlankInfo(traveler);
                index++;
            }
            Console.Write("\r{0}   ", "Importing Table Info...Finished");
        }
        // get a reader friendly string for the color
        private void GetColorInfo(Table traveler)
        {
            // Get the color from the color reference
            for (int row = 2; row < 27; row++)
            {
                var colorRefRange = m_colorRef.get_Range("A" + row, "B" + row);
                if (Convert.ToInt32(colorRefRange.Item[1].Value2) == traveler.ColorNo)
                {
                    traveler.Color = colorRefRange.Item[2].Value2;
                }
                if (colorRefRange != null) Marshal.ReleaseComObject(colorRefRange);
            }
        }
        // calculate how much of each box size
        private void GetBoxInfo(Table traveler)
        {
            for (int row = 2; row < 78; row++)
            {
                var range = m_crossRef.get_Range("B" + row.ToString(), "F" + row.ToString());
                // find the correct model number in the spreadsheet
                if (range.Item[1].Value2 == traveler.ShapeNo)
                {
                    foreach (Order order in traveler.Orders)
                    {
                        // Get box information
                        if (order.ShipVia != "" && (order.ShipVia.ToUpper().IndexOf("FEDEX") != -1 || order.ShipVia.ToUpper().IndexOf("UPS") != -1))
                        {
                            var boxRange = m_boxRef.get_Range("C" + (row + 1), "H" + (row + 1)); // Super Pack
                            traveler.SupPack = (boxRange.Item[1].Value2 != null ? boxRange.Item[5].Value2 + " ( " + boxRange.Item[1].Value2 + " x " + boxRange.Item[2].Value2 + " x " + boxRange.Item[3].Value2 + " )" + (boxRange.Item[4].Value2 != null ? boxRange.Item[4].Value2 + " pads" : "") : "Missing information") + (boxRange.Item[6].Value2 != null ? " " + boxRange.Item[6].Value2 : "");
                            traveler.SupPackQty += order.QuantityOrdered;
                            if (boxRange != null) Marshal.ReleaseComObject(boxRange);
                        }
                        else
                        {
                            var boxRange = m_boxRef.get_Range("I" + (row + 1), "N" + (row + 1)); // Regular Pack
                            traveler.RegPack = (boxRange.Item[1].Value2 != null ? boxRange.Item[5].Value2 + " ( " + boxRange.Item[1].Value2 + " x " + boxRange.Item[2].Value2 + " x " + boxRange.Item[3].Value2 + " )" : "Missing information") + (boxRange.Item[6].Value2 != null ? " " + boxRange.Item[6].Value2 : "");
                            traveler.RegPackQty += order.QuantityOrdered;
                            if (boxRange != null) Marshal.ReleaseComObject(boxRange);
                        }
                    }
                }
                if (range != null) Marshal.ReleaseComObject(range);
            }

        }
        // Calculate how many will be left over + Blank Size
        private void GetBlankInfo(Table traveler) {
            // find the Blank code in the color table
            for (int crow = 2; crow < 27; crow++)
            {
                var colorRange = m_crossRef.get_Range("K" + crow, "M" + crow);
                // find the correct color
                if (Convert.ToInt32(colorRange.Item[1].Value2) == traveler.ColorNo)
                {
                    traveler.BlankColor = colorRange.Item[3].Value2;
                    if (colorRange != null) Marshal.ReleaseComObject(colorRange);
                    break;
                }
                if (colorRange != null) Marshal.ReleaseComObject(colorRange);
            }
            for (int row = 2; row < 78; row++)
            {
                var range = (Excel.Range)m_crossRef.get_Range("B" + row.ToString(), "F" + row.ToString());
                // find the correct model number in the spreadsheet
                if (range.Item[1].Value2 == traveler.ShapeNo)
                {
                    if (range.Item[3].Value2 == "Yes")
                    {
                        
                        // check to see if there is a MAGR blank
                        if (traveler.BlankColor == "MAGR" && range.Item[4].Value2 != null)
                        {

                            traveler.BlankNo = range.Item[4].Value2;
                        }
                        // check to see if there is a CHOK blank
                        else if (traveler.BlankColor == "CHOK" && range.Item[5].Value2 != null)
                        {

                            traveler.BlankNo = range.Item[5].Value2;
                        }
                        // there are no available blanks
                        else
                        {
                            traveler.BlankNo = "";
                        }
                    }
                    if (range != null) Marshal.ReleaseComObject(range);
                }
                if (range != null) Marshal.ReleaseComObject(range);

                var blankRange = m_blankRef.get_Range("A" + row.ToString(), "H" + row.ToString());
                // find the correct model number in the spreadsheet
                if (blankRange.Item[1].Value2 == traveler.ShapeNo)
                {
                    // set the blank size
                    List<int> exceptionColors = new List<int> { 60, 50, 49 };
                    if ((traveler.ShapeNo == "MG2247" || traveler.ShapeNo == "38-2247") && exceptionColors.IndexOf(traveler.ColorNo) != -1)
                    {
                        // Exceptions to the blank parent sheet (certain colors have grain that can't be used with the typical blank)
                        traveler.BlankSize = "(5X10) ~sheet";
                        traveler.BlankNo = "";
                        traveler.PartsPerBlank = 2;
                    }
                    else
                    {
                        // All normal
                        if (Convert.ToInt32(blankRange.Item[7].Value2) > 0)
                        {
                            traveler.BlankSize = "(" + blankRange.Item[8].Value2 + ")";
                            traveler.PartsPerBlank = Convert.ToInt32(blankRange.Item[7].Value2);
                        }
                        else
                        {
                            if (Convert.ToString(blankRange.Item[5].Value2) != "-99999")
                            {
                                traveler.BlankSize = "(" + blankRange.Item[5].Value2 + ") ~sheet";
                            }
                            else
                            {
                                traveler.BlankSize = "No Blank";
                            }
                        }
                    }
                    // calculate production numbers
                    if (traveler.PartsPerBlank < 0) traveler.PartsPerBlank = 0;
                    decimal tablesPerBlank = Convert.ToDecimal(blankRange.Item[7].Value2);
                    if (tablesPerBlank <= 0) tablesPerBlank = 1;
                    traveler.BlankQuantity = Convert.ToInt32(Math.Ceiling(Convert.ToDecimal(traveler.Quantity) / tablesPerBlank));
                    int partsProduced = traveler.BlankQuantity * Convert.ToInt32(tablesPerBlank);
                    traveler.LeftoverParts = partsProduced - traveler.Quantity;
                }
                if (blankRange != null) Marshal.ReleaseComObject(blankRange);


                
            }
            // subtract the inventory parts from the box quantity
            // router.RegPackQty = Math.Max(0, router.RegPackQty - ((router.RegPackQty + router.SupPackQty) - router.Quantity));


            // FROM MAS
            // get bill information from MAS
            //{
            //    OdbcCommand command = MAS.CreateCommand();
            //    command.CommandText = "SELECT CurrentBillRevision, Revision, BlkSize, BlkName, TablesPerBlank FROM BM_billHeader WHERE billno = '" + traveler.PartNo + "'";
            //    OdbcDataReader reader = command.ExecuteReader();
            //    // read info
            //    while (reader.Read())
            //    {
            //        string currentRev = reader.GetString(0);
            //        string thisRev = reader.GetString(1);
            //        // only use the current bill revision
            //        if (currentRev == thisRev) // if (current bill revision == this revision)
            //        {
            //            traveler.BlankSize = reader.GetString(2);
            //            traveler.BlankNo = reader.GetString(3);
            //            traveler.PartsPerBlank = Convert.ToInt32(reader.GetString(4));
            //            break;
            //        }
            //    }
            //    reader.Close();
            //}
        }
        private bool IsTable(string s)
        {
            return (s.Length == 9 && s.Substring(0, 2) == "MG") || (s.Length == 10 && (s.Substring(0, 3) == "38-" || s.Substring(0, 3) == "41-"));
        }

        //-----------------------
        // Properties
        //-----------------------

        private Excel.Worksheet m_crossRef = null;
        private Excel.Worksheet m_boxRef = null;
        private Excel.Worksheet m_blankRef = null;
        private Excel.Worksheet m_colorRef = null;
        private List<Order> m_orders = new List<Order>();
        private List<Table> m_travelers = new List<Table>();
        private OdbcConnection m_MAS = new OdbcConnection();

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

        internal List<Table> Travelers
        {
            get
            {
                return m_travelers;
            }
        }

        internal OdbcConnection MAS
        {
            get
            {
                return m_MAS;
            }

            set
            {
                m_MAS = value;
            }
        }
    }
}
