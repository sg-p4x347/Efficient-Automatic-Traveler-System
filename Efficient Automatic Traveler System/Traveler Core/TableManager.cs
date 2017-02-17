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
                    
                    traveler.BlankSize = row[2];
                    traveler.SheetSize = row[3];
                    // [column 3 contains # of blanks per sheet]
                    traveler.PartsPerBlank = row[5] != "" ? Convert.ToInt32(row[5]) : 0;

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
                    if (traveler.BlankColor == "MAGR" && row[6] != "")
                    {
                        traveler.BlankNo = row[6];
                    }
                    // check to see if there is a CHOK blank
                    else if (traveler.BlankColor == "CHOK" && row[7] != "")
                    {
                        traveler.BlankNo = row[7];
                    }
                    // there are is no specific blank size in the kanban
                    else
                    {
                        traveler.BlankNo = "";
                    }
                    // calculate production numbers
                    if (traveler.PartsPerBlank <= 0) traveler.PartsPerBlank = 1;
                    decimal tablesPerBlank = Convert.ToDecimal(traveler.PartsPerBlank);
                    traveler.BlankQuantity = Convert.ToInt32(Math.Ceiling(Convert.ToDecimal(traveler.Quantity) / tablesPerBlank));
                    int partsProduced = traveler.BlankQuantity * Convert.ToInt32(tablesPerBlank);
                    traveler.LeftoverParts = partsProduced - traveler.Quantity;
                    //--------------------------------------------
                    // PACK & BOX INFO
                    //--------------------------------------------
                    traveler.SupPack = row[8];
                    traveler.RegPack = row[9];
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
                            // approximately 20 max tables per pallet
                            traveler.PalletQty += Convert.ToInt32(Math.Ceiling(Convert.ToDouble(order.QuantityOrdered) / 20));
                        }
                    }
                    //--------------------------------------------
                    // PALLET
                    //--------------------------------------------
                    traveler.PalletSize = row[11];
                    break;
                }
                line = tableRef.ReadLine();
            }
            tableRef.Close();
        }
        
        //-----------------------
        // Properties
        //-----------------------
    }
}
