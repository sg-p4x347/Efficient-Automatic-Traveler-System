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
using System.Net.Http;

namespace Efficient_Automatic_Traveler_System
{
    class TableManager : TravelerManager
    {
        //-----------------------
        // Public members
        //-----------------------
        public TableManager() : base() { }
        public TableManager(OdbcConnection mas) : base(mas)
        {
        }
        //-----------------------
        // Private members
        //-----------------------

        // oversees the importing of externally stored information
        protected override void ImportInformation()
        {
            int index = 0;
            foreach (Table table in m_travelers.OfType<Table>())
            {
                if (table.Part == null) table.ImportPart(MAS);
                Server.Write("\r{0}%", "Importing Table Info..." + Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(m_travelers.Count)) * 100));
                //table.CheckInventory(MAS);!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!TEMP-- UNCOMMENT AFTER TESTING!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

                // update and total the final parts
                table.Part.TotalQuantity = table.Quantity;
                table.FindComponents(table.Part);
                // Table specific (Color, blank info, and box dimensions)
                GetColorInfo(table);
                GetTableInfo(table);
                index++;
            }
            Server.Write("\r{0}", "Importing Table Info...Finished" + Environment.NewLine);
        }
        // get a reader friendly string for the color
        private void GetColorInfo(Table traveler)
        {
            // open the color ref csv file
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            System.IO.StreamReader colorRef = new StreamReader(System.IO.Path.Combine(exeDir, "Color Reference.csv"));
            colorRef.ReadLine(); // read past the header
            string line = colorRef.ReadLine();
            while (line != "")
            {
                string[] row = line.Split(',');
                if (Convert.ToInt32(row[0]) == traveler.ColorNo)
                {
                    traveler.Color = row[1];
                    traveler.BlankColor = row[2];
                    break;
                }
                line = colorRef.ReadLine();
            }
            colorRef.Close();
        }
        // calculate how much of each box size
        private void GetTableInfo(Table traveler)
        {
            // open the table ref csv file
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            System.IO.StreamReader tableRef = new StreamReader(System.IO.Path.Combine(exeDir, "Table Reference.csv"));
            tableRef.ReadLine(); // read past the header
            string line = tableRef.ReadLine();
            while (line != "")
            {
                string[] row = line.Split(',');
                if (row[0] == traveler.ShapeNo)
                {
                    //--------------------------------------------
                    // BLANK INFO
                    //--------------------------------------------
                    
                    traveler.BlankSize = row[1];
                    traveler.SheetSize = row[2];
                    // [column 3 contains # of blanks per sheet]
                    traveler.PartsPerBlank = row[4] != "" ? Convert.ToInt32(row[4]) : 0;

                    // Exception cases -!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!
                    List<int> exceptionColors = new List<int> { 60, 50, 49 };
                    if ((traveler.ShapeNo == "MG2247" || traveler.ShapeNo == "38-2247") && exceptionColors.IndexOf(traveler.ColorNo) != -1)
                    {
                        // Exceptions to the blank parent sheet (certain colors have grain that can't be used with the typical blank)
                        traveler.BlankComment = "Use " + traveler.SheetSize + " sheet and align grain";
                        traveler.PartsPerBlank = 2;
                    }
                    //!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!

                    // check to see if there is a MAGR blank
                    if (traveler.BlankColor == "MAGR" && row[5] != "")
                    {
                        traveler.BlankNo = row[5];
                    }
                    // check to see if there is a CHOK blank
                    else if (traveler.BlankColor == "CHOK" && row[6] != "")
                    {
                        traveler.BlankNo = row[6];
                    }
                    // there are is no specific blank size in the kanban
                    else
                    {
                        traveler.BlankNo = "";
                    }
                    //--------------------------------------------
                    // PACK & BOX INFO
                    //--------------------------------------------
                    traveler.SupPack = row[7];
                    traveler.RegPack = row[8];
                    foreach (Order order in traveler.Orders)
                    {
                        // Get box information
                        if (order.ShipVia != "" && (order.ShipVia.ToUpper().IndexOf("FEDEX") != -1 || order.ShipVia.ToUpper().IndexOf("UPS") != -1))
                        {
                            traveler.SupPackQty += order.QuantityOrdered;
                        }
                        else
                        {
                            traveler.RegPackQty += order.QuantityOrdered;
                        }
                    }
                    break;
                }
                line = tableRef.ReadLine();
            }
            tableRef.Close();
        }
        // Calculate how many will be left over + Blank Size
        //private void GetBlankInfo(Table traveler)
        //{
        //    // find the Blank code in the color table
        //    for (int crow = 2; true; crow++)
        //    {
        //        var colorRange = m_crossRef.get_Range("K" + crow, "M" + crow);
        //        if (Convert.ToString(colorRange.Value2) == "") break;
        //        // find the correct color
        //        if (Convert.ToInt32(colorRange.Item[1].Value2) == traveler.ColorNo)
        //        {
        //            traveler.Color = colorRange.Item[2].Value2;
        //            traveler.BlankColor = colorRange.Item[3].Value2;
        //            if (colorRange != null) Marshal.ReleaseComObject(colorRange);
        //            break;
        //        }
        //        if (colorRange != null) Marshal.ReleaseComObject(colorRange);
        //    }
        //    for (int row = 2; row < 78; row++)
        //    {
        //        var range = (Excel.Range)m_crossRef.get_Range("B" + row.ToString(), "F" + row.ToString());
        //        // find the correct model number in the spreadsheet
        //        if (range.Item[1].Value2 == traveler.ShapeNo)
        //        {
        //            if (range.Item[3].Value2 == "Yes")
        //            {

        //                // check to see if there is a MAGR blank
        //                if (traveler.BlankColor == "MAGR" && range.Item[4].Value2 != null)
        //                {
        //                    traveler.BlankNo = range.Item[4].Value2;
        //                }
        //                // check to see if there is a CHOK blank
        //                else if (traveler.BlankColor == "CHOK" && range.Item[5].Value2 != null)
        //                {

        //                    traveler.BlankNo = range.Item[5].Value2;
        //                }
        //                // there are no available blanks
        //                else
        //                {
        //                    traveler.BlankNo = "";
        //                }
        //            }
        //            if (range != null) Marshal.ReleaseComObject(range);
        //        }
        //        if (range != null) Marshal.ReleaseComObject(range);

        //        var blankRange = m_blankRef.get_Range("A" + row.ToString(), "H" + row.ToString());
        //        // find the correct model number in the spreadsheet
        //        if (blankRange.Item[1].Value2 == traveler.ShapeNo)
        //        {
        //            // set the blank size
        //            List<int> exceptionColors = new List<int> { 60, 50, 49 };
        //            if ((traveler.ShapeNo == "MG2247" || traveler.ShapeNo == "38-2247") && exceptionColors.IndexOf(traveler.ColorNo) != -1)
        //            {
        //                // Exceptions to the blank parent sheet (certain colors have grain that can't be used with the typical blank)
        //                traveler.BlankSize = "(5X10)";
        //                traveler.BlankNo = "";
        //                traveler.PartsPerBlank = 2;
        //            }
        //            else
        //            {
        //                // Blank
        //                if (Convert.ToInt32(blankRange.Item[7].Value2) > 0)
        //                {
        //                    traveler.BlankSize += "(" + blankRange.Item[8].Value2 + ")";
        //                    traveler.PartsPerBlank = Convert.ToInt32(blankRange.Item[7].Value2);
        //                }
        //                // sheet
        //                if (blankRange.Item[5].Value2 != null)
        //                {
        //                    traveler.BlankSize += "(" + blankRange.Item[5].Value2 + ")";
        //                }
        //            }
        //            // calculate production numbers
        //            if (traveler.PartsPerBlank < 0) traveler.PartsPerBlank = 0;
        //            decimal tablesPerBlank = Convert.ToDecimal(blankRange.Item[7].Value2);
        //            if (tablesPerBlank <= 0) tablesPerBlank = 1;
        //            traveler.BlankQuantity = Convert.ToInt32(Math.Ceiling(Convert.ToDecimal(traveler.Quantity) / tablesPerBlank));
        //            int partsProduced = traveler.BlankQuantity * Convert.ToInt32(tablesPerBlank);
        //            traveler.LeftoverParts = partsProduced - traveler.Quantity;
        //        }
        //        if (blankRange != null) Marshal.ReleaseComObject(blankRange);



        //    }
        //    // subtract the inventory parts from the box quantity
        //    // router.RegPackQty = Math.Max(0, router.RegPackQty - ((router.RegPackQty + router.SupPackQty) - router.Quantity));


        //    // FROM MAS
        //    // get bill information from MAS
        //    //{
        //    //    OdbcCommand command = MAS.CreateCommand();
        //    //    command.CommandText = "SELECT CurrentBillRevision, Revision, BlkSize, BlkName, TablesPerBlank FROM BM_billHeader WHERE billno = '" + traveler.PartNo + "'";
        //    //    OdbcDataReader reader = command.ExecuteReader();
        //    //    // read info
        //    //    while (reader.Read())
        //    //    {
        //    //        string currentRev = reader.GetString(0);
        //    //        string thisRev = reader.GetString(1);
        //    //        // only use the current bill revision
        //    //        if (currentRev == thisRev) // if (current bill revision == this revision)
        //    //        {
        //    //            traveler.BlankSize = reader.GetString(2);
        //    //            traveler.BlankNo = reader.GetString(3);
        //    //            traveler.PartsPerBlank = Convert.ToInt32(reader.GetString(4));
        //    //            break;
        //    //        }
        //    //    }
        //    //    reader.Close();
        //    //}
        //}

        //-----------------------
        // Properties
        //-----------------------
    }
}
