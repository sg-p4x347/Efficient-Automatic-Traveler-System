using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.Odbc;
using System.Diagnostics;
using System.IO;
using Excel = Microsoft.Office.Interop.Excel;
using Marshal = System.Runtime.InteropServices.Marshal;
using System.Drawing.Printing;
using System.Net.Http;

namespace Efficient_Automatic_Traveler_System
{
    class TableManager
    {
        public TableManager() { }
        public TableManager(OdbcConnection mas, Label infoLabel,ProgressBar progressBar, ListView listview, Excel.Worksheet crossRef, Excel.Worksheet boxRef, Excel.Worksheet blankRef, Excel.Worksheet colorRef, bool checkPrinted = true)
        {
            m_MAS = mas;
            m_infoLabel = infoLabel;
            m_progressBar = progressBar;
            m_tableListView = listview;
            m_crossRef = crossRef;
            m_boxRef = boxRef;
            m_blankRef = blankRef;
            m_colorRef = colorRef;
            m_checkPrinted = checkPrinted;
        }
        //=======================
        // Travelers
        //=======================
        public void CompileTravelers(BackgroundWorker backgroundWorker1,Mode mode,string specificID)
        {
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            // clear any previous travelers
            m_travelers.Clear();
            // get the list of travelers that have been printed
            List<Traveler> printedTravelers = new List<Traveler>();
            //==========================================
            // Remove any orders that have been printed
            //==========================================
            if (m_checkPrinted || mode == Mode.CreatePrinted) {
                string line;
                System.IO.StreamReader file = new System.IO.StreamReader(System.IO.Path.Combine(exeDir, "printed.json"));
                while ((line = file.ReadLine()) != null && line != "")
                {
                    Traveler printedTraveler = new Traveler(line);
                    switch (mode)
                    {
                        case Mode.CreatePrinted:
                            // just add this traveler to the finished list
                            if (IsTable(printedTraveler.PartNo)) Travelers.Add(new Table(line));
                            break;
                        case Mode.CreateSpecific:
                            if (printedTraveler.ID.ToString("D6") == specificID && IsTable(printedTraveler.PartNo))
                            {
                                Travelers.Add(new Table(line));
                                break;
                            }
                            goto default;
                        default:
                            // check to see if these orders have been printed already
                            foreach (Order printedOrder in printedTraveler.Orders)
                            {
                                foreach (Order order in m_orders)
                                {
                                    if (order.SalesOrderNo == printedOrder.SalesOrderNo && order.ItemCode == printedOrder.ItemCode)
                                    {
                                        // throw this order out
                                        if (mode != Mode.CreateSpecific)
                                        {
                                            m_orders.Remove(order);
                                            break;
                                        }
                                    }
                                }
                            }
                            break;
                    }
                }
                file.Close();
            }
            if (mode != Mode.CreatePrinted)
            {
                //==========================================
                // compile the travelers
                //==========================================
                
                int index = 0;
                foreach (Order order in m_orders)
                {
                    backgroundWorker1.ReportProgress(Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(m_orders.Count)) * 100), "Compiling Tables...");
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
            }
            ImportInformation(backgroundWorker1);
        }
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
        private void ImportInformation(BackgroundWorker backgroundWorker1)
        { 
            int index = 0;
            foreach (Table traveler in m_travelers)
            {
                if (traveler.Part == null) traveler.ImportPart(MAS);
                backgroundWorker1.ReportProgress(Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(m_travelers.Count)) * 100), "Gathering Table Info...");
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
                            if (blankRange.Item[5].Value2 != "-99999")
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
        public void DisplayTravelers()
        {
            // display the results to the chairListView
            m_tableListView.Clear();
            // Set to details view.
            m_tableListView.View = View.Details;

            // production info
            m_tableListView.Columns.Add("Part No.", 150, HorizontalAlignment.Left);
            m_tableListView.Columns.Add("ID", 100, HorizontalAlignment.Left);
            m_tableListView.Columns.Add("Ordered", 100, HorizontalAlignment.Left);
            m_tableListView.Columns.Add("Need to Produce", 100, HorizontalAlignment.Left);
            // order info
            m_tableListView.Columns.Add("Order No.(s)", 200, HorizontalAlignment.Left);
            m_tableListView.Columns.Add("Customer(s)", 200, HorizontalAlignment.Left);
            m_tableListView.Columns.Add("Ship date(s)", 200, HorizontalAlignment.Left);


            foreach (Table traveler in m_travelers)
            {
                string dateList = "";
                string customerList = "";
                string orderList = "";
                int totalOrdered = 0;
                int i = 0;
                foreach (Order order in traveler.Orders)
                {
                    totalOrdered += order.QuantityOrdered;
                    dateList += (i == 0 ? "" : ", ") + order.OrderDate.ToString("MM/dd/yyyy");
                    customerList += (i == 0 ? "" : ", ") + order.CustomerNo;
                    orderList += (i == 0 ? "" : ", ") + order.SalesOrderNo;
                    i++;
                }
                string[] row = {
                    traveler.Part.BillNo,
                    traveler.ID.ToString("D6"),
                    totalOrdered.ToString(),
                    traveler.Quantity.ToString(),
                    orderList,
                    customerList,
                    dateList
                };
                ListViewItem tableListViewItem = new ListViewItem(row);
                tableListViewItem.Checked = true;
                m_tableListView.Items.Add(tableListViewItem);
            }
            m_tableListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
            m_tableListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }
        //=======================
        // Printing
        //=======================
        public void PrintTravelers(Excel.Sheets worksheets)
        {
            m_infoLabel.Text = "Printing Table Travelers...";
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            // open printed log file
            System.IO.StreamWriter file = File.AppendText(System.IO.Path.Combine(exeDir, "printed.json"));

            // create the output workbook
            for (int itemIndex = 0; itemIndex < m_tableListView.Items.Count; itemIndex++)
            {
                if (m_tableListView.Items[itemIndex].Checked)
                {
                    Table traveler = m_travelers[itemIndex];

                    // copy the sheet
                    worksheets.get_Item("Table").Copy(Type.Missing, worksheets[worksheets.Count]);
                    Excel.Worksheet outputSheet = worksheets[worksheets.Count];

                    // Sales Orders
                    string customerList = "";
                    string orderList = "";
                    int i = 0;
                    foreach (Order order in traveler.Orders)
                    {
                        customerList += (i == 0 ? "" : ", ") + order.CustomerNo;
                        orderList += (i == 0 ? "" : ", ") + "(" + order.QuantityOrdered + ") " + order.SalesOrderNo;
                        i++;
                    }
                    //#####################
                    // Production Traveler
                    //#####################
                    Excel.Range range;
                    int row = 1;
                    // Documentation
                    range = outputSheet.get_Range("A" + row, "A" + row);
                    range.Value2 = traveler.ID.ToString("D6") + (traveler.Printed ? " COPY" : "");
                    range.get_Characters(7, 15).Font.FontStyle = "bold";
                    range.get_Characters(7, 15).Font.Size = 20;
                    row++;
                    // Part
                    range = outputSheet.get_Range("B" + row, "C" + row);
                    range.Item[1] = traveler.Part.BillNo;
                    range.Item[2] = traveler.Quantity;
                    row++;
                    // Description
                    range = outputSheet.get_Range("B" + row, "B" + row);
                    range.Value2 = traveler.Part.BillDesc;
                    row++;
                    // Drawing
                    range = outputSheet.get_Range("B" + row, "B" + row);
                    range.Value2 = traveler.Part.DrawingNo;
                    row++;
                    // Sales Orders
                    range = outputSheet.get_Range("B" + row, "C" + row);
                    range.Item[1].Value2 = orderList;
                    row++;
                    // Customers
                    range = outputSheet.get_Range("B" + row, "C" + row);
                    range.Item[1].Value2 = customerList;
                    row++;
                    // Date printed
                    traveler.TimeStamp = DateTime.Now.ToString("MM/dd/yyyy   hh:mm tt");
                    range = outputSheet.get_Range("B" + row, "B" + row);
                    range.Value2 = traveler.TimeStamp;
                    row++;
                    // Blank
                    range = outputSheet.get_Range("B" + row, "C" + row);
                    range.Item[1].Value2 = traveler.BlankNo + "   " + traveler.BlankSize + " (" + traveler.PartsPerBlank + " per blank)";
                    range.Item[2].Value2 = traveler.BlankQuantity;
                    row++;
                    // Leftover
                    range = outputSheet.get_Range("B" + row, "C" + row);
                    range.Item[2].Value2 = traveler.LeftoverParts;
                    row++;
                    // Parent material
                    range = outputSheet.get_Range("B" + row, "C" + row);
                    range.Item[1].Value2 = traveler.Material.ItemCode;
                    range.Item[2].Value2 = traveler.Material.QuantityPerBill + " " + traveler.Material.Unit;
                    row++;
                    // Color
                    range = outputSheet.get_Range("B" + row, "B" + row);
                    range.Value2 = traveler.Color;
                    row++;
                    // Heien/Weeke rate
                    range = outputSheet.get_Range("B" + row, "C" + row);
                    range.Item[1].Value2 = traveler.Cnc.QuantityPerBill + " " + traveler.Cnc.Unit;
                    range.Item[2].Value2 = traveler.Cnc.QuantityPerBill * traveler.Quantity + " " + traveler.Vector.Unit;
                    row++;
                    // Vector rate
                    range = outputSheet.get_Range("B" + row, "C" + row);
                    range.Item[1].Value2 = traveler.Vector.QuantityPerBill + " " + traveler.Vector.Unit;
                    range.Item[2].Value2 = traveler.Vector.QuantityPerBill * traveler.Quantity + " " + traveler.Vector.Unit;
                    row++;
                    // Pack rate
                    if (traveler.Assm != null)
                    {
                        range = outputSheet.get_Range("B" + row, "C" + row);
                        range.Item[1].Value2 = traveler.Assm.QuantityPerBill + " " + traveler.Assm.Unit;
                        range.Item[2].Value2 = traveler.Assm.QuantityPerBill * traveler.Quantity + " " + traveler.Vector.Unit;
                    }
                    row++;
                    // Regular pack
                    range = outputSheet.get_Range("B" + row, "C" + row);
                    range.Item[1].Value2 = traveler.RegPack + (traveler.BoxItemCode != "" ? " Or box: " + traveler.BoxItemCode : "");
                    range.Item[2].Value2 = traveler.RegPackQty;
                    row++;
                    // Super pack
                    range = outputSheet.get_Range("B" + row, "C" + row);
                    range.Item[1].Value2 = traveler.SupPack;
                    range.Item[2].Value2 = traveler.SupPackQty;
                    row++;
                    // Hardware components
                    range = outputSheet.get_Range("B" + row, "B" + row);
                    string hardware = "";
                    foreach (Item component in traveler.Components)
                    {
                        hardware += (hardware.Length > 0 ? "," : "") + "(" + component.TotalQuantity + ") " + component.ItemCode;
                    }
                    range.Value2 = hardware;
                    row++;

                    //#####################
                    // Box Construction
                    //#####################

                    row = 21;
                    // Documentation
                    range = outputSheet.get_Range("A" + row, "A" + row);
                    range.Value2 = traveler.ID.ToString("D6") + (traveler.Printed ? " COPY" : "");
                    range.get_Characters(7, 15).Font.FontStyle = "bold";
                    range.get_Characters(7, 15).Font.Size = 20;
                    row++;
                    // Part
                    range = outputSheet.get_Range("B" + row, "C" + row);
                    range.Item[1] = traveler.Part.BillNo;
                    range.Item[2] = traveler.Quantity;
                    row++;
                    // Description
                    range = outputSheet.get_Range("B" + row, "B" + row);
                    range.Value2 = traveler.Part.BillDesc;
                    row++;
                    // Regular pack
                    range = outputSheet.get_Range("B" + row, "C" + row);
                    range.Item[1].Value2 = traveler.RegPack + (traveler.BoxItemCode != "" ? " Or box: " + traveler.BoxItemCode : "");
                    range.Item[2].Value2 = traveler.RegPackQty;
                    row++;
                    // Super pack
                    range = outputSheet.get_Range("B" + row, "C" + row);
                    range.Item[1].Value2 = traveler.SupPack;
                    range.Item[2].Value2 = traveler.SupPackQty;
                    row++;
                    // Box rate
                    if (traveler.Box != null)
                    {
                        range = outputSheet.get_Range("B" + row, "C" + row);
                        range.Item[1].Value2 = traveler.Box.QuantityPerBill + " " + traveler.Box.Unit;
                        range.Item[2].Value2 = traveler.Box.QuantityPerBill * traveler.Quantity + " " + traveler.Vector.Unit;
                    }
                    row++;
                    // Sales Orders
                    range = outputSheet.get_Range("B" + row, "C" + row);
                    range.Item[1].Value2 = orderList;
                    row++;
                    // Customers
                    range = outputSheet.get_Range("B" + row, "C" + row);
                    range.Item[1].Value2 = customerList;
                    row++;
                    // Date printed
                    range = outputSheet.get_Range("B" + row, "B" + row);
                    range.Value2 = traveler.TimeStamp;
                    row++;
                    try
                    {
                        //##### Print the Cover sheet #######
                        outputSheet.PrintOut(
                            Type.Missing, Type.Missing, Type.Missing, Type.Missing,
                            Type.Missing, Type.Missing, Type.Missing, Type.Missing);
                        //###################################

                        // successfully printed, so we should log in the printed.json file
                        if (!traveler.Printed)
                        {
                            file.Write(traveler.Export());
                            file.Flush();
                        }
                        traveler.Printed = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("A problem occured when printing table travelers: " + ex.Message);
                    }
                }
            }
            file.Close();
            m_infoLabel.Text = "";
        }
        async public void PrintLabels()
        {
            foreach (Table table in m_travelers) {
                using (var client = new HttpClient())
                {
                    var values = new Dictionary<string, string>
                    {
                       { "Unit", table.PartNo },
                       { "Quantity", table.Quantity.ToString() }
                    };

                    var content = new FormUrlEncodedContent(values);

                    var response = await client.PostAsync("http://192.168.2.6:8080/", content);

                    var responseString = await response.Content.ReadAsStringAsync();
                }
            }
        }
        //=======================
        // Properties
        //=======================
        private ListView m_tableListView = null;
        private Label m_infoLabel = null;
        private ProgressBar m_progressBar = null;
        private Excel.Worksheet m_crossRef = null;
        private Excel.Worksheet m_boxRef = null;
        private Excel.Worksheet m_blankRef = null;
        private Excel.Worksheet m_colorRef = null;
        private bool m_checkPrinted = true;
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
