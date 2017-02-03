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
    class TableManager : TravelerManager
    {
        //-----------------------
        // Public members
        //-----------------------
        public TableManager() : base() { }
        public TableManager(OdbcConnection mas, Excel.Worksheet crossRef, Excel.Worksheet boxRef, Excel.Worksheet blankRef, Excel.Worksheet colorRef) : base(mas)
        {
            m_crossRef = crossRef;
            m_boxRef = boxRef;
            m_blankRef = blankRef;
            m_colorRef = colorRef;
        }
        //-----------------------
        // Private members
        //-----------------------
        protected override void ImportInformation()
        {
            int index = 0;
            foreach (Table table in m_travelers.OfType<Table>())
            {
                if (table.Part == null) table.ImportPart(MAS);
                Console.Write("\r{0}%", "Importing Table Info..." + Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(m_travelers.Count)) * 100));
                table.CheckInventory(MAS);
                // update and total the final parts
                table.Part.TotalQuantity = table.Quantity;
                table.FindComponents(table.Part);
                // Table specific
                GetColorInfo(table);
                GetBoxInfo(table);
                GetBlankInfo(table);
                index++;
            }
            Console.Write("\r{0}", "Importing Table Info...Finished\n");
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

        //-----------------------
        // Properties
        //-----------------------

        private Excel.Worksheet m_crossRef = null;
        private Excel.Worksheet m_boxRef = null;
        private Excel.Worksheet m_blankRef = null;
        private Excel.Worksheet m_colorRef = null;
    }
}
