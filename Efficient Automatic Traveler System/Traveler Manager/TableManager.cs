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
        public TableManager(ref OdbcConnection mas, ITravelerManager travelerCore) : base(ref mas, travelerCore)
        {
        }
        //public override void CompileTravelers(ref List<Order> newOrders)
        //{
        //    int index = 0;
        //    for (int orderIndex = 0; orderIndex < newOrders.Count;orderIndex++)
        //    {
        //        Order order = newOrders[orderIndex];
        //        for (int itemIndex = 0; itemIndex < order.Items.Count; itemIndex++)
        //        {
        //            OrderItem item = order.Items[itemIndex];
        //            // only make a traveler if this one has no child traveler already (-1 signifies no child traveler)
        //            if (item.ChildTraveler < 0 && Traveler.IsTable(item.ItemCode))
        //            {
        //                Console.Write("\r{0}%   ", "Compiling Tables..." + Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(newOrders.Count)) * 100));

        //                // search for existing traveler
        //                // can only combine if same itemCode, hasn't started, and has no parents
        //                Traveler traveler = m_travelers.Find(x => x.ItemCode == item.ItemCode && x.LastStation == Traveler.GetStation("Start") && x.Parents.Count == 0);
        //                if (traveler != null)
        //                {
        //                    // add to existing traveler
        //                    traveler.Quantity += item.QtyOrdered;

        //                    // RELATIONAL =============================================================
        //                    item.ChildTraveler = traveler.ID;
        //                    traveler.ParentOrders.Add(order.SalesOrderNo);
        //                    //=========================================================================
        //                }
        //                else 
        //                {
        //                    // create a new traveler from the new item
        //                    Table newTraveler = new Table(item.ItemCode, item.QtyOrdered, ref m_MAS);

        //                    // RELATIONAL =============================================================
        //                    item.ChildTraveler = newTraveler.ID;
        //                    newTraveler.ParentOrders.Add(order.SalesOrderNo);
        //                    //=========================================================================

        //                    // start the new traveler's journey
        //                    newTraveler.Start();
        //                    // add the new traveler to the list
        //                    m_travelers.Add(newTraveler);
        //                }
        //            }
        //        }
        //        index++;
        //    }
        //    Console.Write("\r{0}   ", "Compiling Tables...Finished\n");
        //}
        // Import information for all tables
        public override void FinalizeTravelers()
        {
            int index = 0;
            List<Traveler> preCulled = new List<Traveler>(m_travelerCore.GetTravelers.OfType<Table>());
            foreach (Table table in preCulled)
            {
                if (table.Part == null) table.ImportPart(ref m_MAS);
                Server.Write("\r{0}%", "Importing Table Info..." + Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(m_travelerCore.GetTravelers.Count)) * 100));
                FinalizeTable(table);
                index++;
            }
            Server.Write("\r{0}", "Importing Table Info...Finished" + Environment.NewLine);
        }
        // Import information for a specific table
        public void FinalizeTable(Table table)
        {
            // compensate for items covered by inventory (already calculated for the order item)
            UpdateQuantity(table);
            // only update quantity and blank information if it hasn't started (probably sitting at the Heian)
            if (table.LastStation == Traveler.GetStation("Start"))
            {
                // get blank information and calculate the actual production quantity
                GetBlankInfo(table);
                table.Quantity += table.LeftoverParts;
                // update and total the final parts
                table.Part.TotalQuantity = table.Quantity;
                
            }
            if (table.Quantity == 0)
            {
                m_travelerCore.RemoveTraveler(table);
            }
            else
            {
                table.FindComponents(table.Part);
                // Table specific (Color and box dimensions)
                GetColorInfo(table);
                GetPackInfo(table);
            }
        }
        //-----------------------
        // Private members
        //-----------------------
        //protected override bool InstanceOf(string itemCode)
        //{
        //    return Traveler.IsTable(itemCode);
        //}
        //protected override Traveler NewDerivedInstance(string itemCode, int qty, ref OdbcConnection mas)
        //{
        //    return (Traveler)new Table(itemCode, qty, ref mas);
        //}
        // get a reader friendly string for the color
        private void GetColorInfo(Table traveler)
        {
            // open the color ref csv file
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            System.IO.StreamReader colorRef = new StreamReader(System.IO.Path.Combine(exeDir, "Color Reference.csv"));
            colorRef.ReadLine(); // read past the header
            string line = colorRef.ReadLine();
            while (line != "" && line != null)
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
        // calculate how many actual tables will be produced from the blanks
        private void GetBlankInfo(Table traveler)
        {
            // open the table ref csv file
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            System.IO.StreamReader tableRef = new StreamReader(System.IO.Path.Combine(exeDir, "Table Reference.csv"));
            tableRef.ReadLine(); // read past the header
            string line = tableRef.ReadLine();
            while (line != "" && line != null)
            {
                string[] row = line.Split(',');
                if (traveler.ItemCode.Contains(row[0]))
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
                }
                line = tableRef.ReadLine();
            }
            tableRef.Close();
        }
        // calculate how much of each box size
        private void GetPackInfo(Table traveler)
        {
            // open the table ref csv file
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            System.IO.StreamReader tableRef = new StreamReader(System.IO.Path.Combine(exeDir, "Table Reference.csv"));
            tableRef.ReadLine(); // read past the header
            string line = tableRef.ReadLine();
            while (line != "" && line != null)
            {
                string[] row = line.Split(',');
                if (row[0] == traveler.ShapeNo)
                {
                    //--------------------------------------------
                    // PACK & BOX INFO
                    //--------------------------------------------
                    traveler.SupPack = row[8];
                    traveler.RegPack = row[9];
                    foreach (string orderNo in traveler.ParentOrders)
                    {
                        Order order = m_travelerCore.FindOrder(orderNo);
                        OrderItem orderItem = order.FindItem(traveler.ID);

                        // Get box information
                        if (order.ShipVia != "" && (order.ShipVia.ToUpper().IndexOf("FEDEX") != -1 || order.ShipVia.ToUpper().IndexOf("UPS") != -1))
                        {
                            traveler.SupPackQty += orderItem.QtyOrdered;
                        }
                        else
                        {
                            traveler.RegPackQty += orderItem.QtyOrdered;
                            // approximately 20 max tables per pallet
                            traveler.PalletQty += Convert.ToInt32(Math.Ceiling(Convert.ToDouble(orderItem.QtyOrdered) / 20));
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
