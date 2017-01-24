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
    class ChairManager
    {
        public ChairManager() { }
        public ChairManager(OdbcConnection mas) {
            m_MAS = mas;
            m_infoLabel = infoLabel;
            m_progressBar = progressBar;
            m_chairListView = listview;
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
            string line;
            System.IO.StreamReader file = new System.IO.StreamReader(System.IO.Path.Combine(exeDir, "printed.json"));
            while ((line = file.ReadLine()) != null && line != "")
            {
                Traveler printedTraveler = new Traveler(line);
                switch (mode)
                {
                    case Mode.CreatePrinted:
                        // just add this traveler to the finished list
                        if (IsChair(printedTraveler.PartNo)) Travelers.Add(new Chair(line));
                        break;
                    case Mode.CreateSpecific:
                        if (printedTraveler.ID.ToString("D6") == specificID && IsChair(printedTraveler.PartNo))
                        {
                            Travelers.Add(new Chair(line));
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
            if (mode != Mode.CreatePrinted)
            {
                //==========================================
                // compile the travelers
                //==========================================
                int index = 0;
                foreach (Order order in m_orders)
                {
                    backgroundWorker1.ReportProgress(Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(m_orders.Count)) * 100), "Compiling Chairs...");
                    // Make a unique traveler for each order, while combining common parts from different models into single traveler
                    bool foundBill = false;
                    // search for existing traveler
                    foreach (Chair traveler in m_travelers)
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
                        Chair newTraveler = new Chair(order.ItemCode, order.QuantityOrdered, MAS);
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
        private void ImportInformation(BackgroundWorker backgroundWorker1)
        {
            int index = 0;
            foreach (Chair traveler in m_travelers)
            {
                if (traveler.Part == null) traveler.ImportPart(MAS);
                backgroundWorker1.ReportProgress(Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(m_travelers.Count)) * 100), "Gathering Chair Info...");
                traveler.CheckInventory(MAS);
                // update and total the final parts
                traveler.Part.TotalQuantity = traveler.Quantity;
                traveler.FindComponents(traveler.Part);
                // chair specific
                GetBoxInfo(traveler);
            }
        }
        private void GetBoxInfo(Chair traveler)
        {
            if (traveler.PartNo[traveler.PartNo.Length-1] == '4')
            {
                traveler.PartsPerBox = 4;
            } else
            {
                traveler.PartsPerBox = 6;
            }
            traveler.RegPackQty = traveler.Quantity / traveler.PartsPerBox;
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
        public void DisplayTravelers()
        {
            // display the results to the chairListView
            m_chairListView.Clear();
            // Set to details view.
            m_chairListView.View = View.Details;

            // production info
            m_chairListView.Columns.Add("Part No.", 100, HorizontalAlignment.Left);
            m_chairListView.Columns.Add("ID", 50, HorizontalAlignment.Left);
            m_chairListView.Columns.Add("Ordered", 75, HorizontalAlignment.Left);
            m_chairListView.Columns.Add("Need to Produce", 75, HorizontalAlignment.Left);
            // order info
            m_chairListView.Columns.Add("Order No.(s)", 200, HorizontalAlignment.Left);
            m_chairListView.Columns.Add("Customer(s)", 200, HorizontalAlignment.Left);
            m_chairListView.Columns.Add("Ship date(s)", 100, HorizontalAlignment.Left);
            

            foreach (Chair traveler in m_travelers)
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
                ListViewItem chairListViewItem = new ListViewItem(row);
                chairListViewItem.Checked = true;
                m_chairListView.Items.Add(chairListViewItem);
            }
            m_chairListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
            m_chairListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }
        //=======================
        // Printing
        //=======================
        public void PrintTravelers(Excel.Sheets worksheets)
        {
            m_infoLabel.Text = "Printing Chair Travelers...";
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            // open printed log file
            System.IO.StreamWriter file = File.AppendText(System.IO.Path.Combine(exeDir, "printed.json"));

            // create the output workbook
            for (int itemIndex = 0; itemIndex < m_chairListView.Items.Count; itemIndex++)
            {
                if (m_chairListView.Items[itemIndex].Checked)
                {
                    Chair traveler = m_travelers[itemIndex];

                    // copy the sheet
                    worksheets.get_Item("Chair").Copy(Type.Missing, worksheets[worksheets.Count]);
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
                    // Part -----------------------------------------------------------------
                    range = outputSheet.get_Range("B" + row, "C" + row);
                    range.Item[1] = traveler.Part.BillNo;
                    range.Item[2] = traveler.Quantity;
                    row++;
                    // Description
                    range = outputSheet.get_Range("B" + row, "B" + row);
                    range.Value2 = traveler.Part.BillDesc;
                    row++;
                    // Sales Orders -----------------------------------------------------------------
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
                    // Assembly -----------------------------------------------------------------
                    range = outputSheet.get_Range("B" + row, "C" + row);
                    if (traveler.Assm != null)
                    {
                        range.Item[1].Value2 = traveler.Assm.QuantityPerBill + " " + traveler.Assm.Unit;
                        range.Item[2].Value2 = traveler.Assm.TotalQuantity + " " + traveler.Assm.Unit;
                    }
                    else
                    {
                        range.Item[1].Value2 = "N/A";
                    }
                    row++;
                    // Regular pack
                    range = outputSheet.get_Range("B" + row, "C" + row);
                    range.Item[1].Value2 = (traveler.BoxItemCode == "" ? traveler.RegPack : "Use box: " + traveler.BoxItemCode);
                    range.Item[2].Value2 = traveler.RegPackQty;
                    row+=2;
                    // Components ---------------------------------------------------------------
                    int startRow = row;
                    foreach (Item component in traveler.Components)
                    {
                        range = outputSheet.get_Range("A" + row, "C" + row);
                        range.Item[1].Value2 = component.ItemCode;
                        range.Item[2].Value2 = component.ItemCodeDesc;
                        range.Item[3].Value2 = component.TotalQuantity.ToString();
                        row++;
                    }
                    range = outputSheet.get_Range("A" + startRow, "C" + (row-1));
                    Excel.Borders borders = range.Borders;
                    borders[Excel.XlBordersIndex.xlInsideHorizontal].LineStyle = Excel.XlLineStyle.xlContinuous;
                    borders[Excel.XlBordersIndex.xlInsideVertical].LineStyle = Excel.XlLineStyle.xlContinuous;
                    borders[Excel.XlBordersIndex.xlEdgeTop].LineStyle = Excel.XlLineStyle.xlContinuous;
                    borders[Excel.XlBordersIndex.xlEdgeLeft].LineStyle = Excel.XlLineStyle.xlContinuous;
                    borders[Excel.XlBordersIndex.xlEdgeBottom].LineStyle = Excel.XlLineStyle.xlContinuous;
                    borders[Excel.XlBordersIndex.xlEdgeRight].LineStyle = Excel.XlLineStyle.xlContinuous;

                    //#####################
                    // Box Construction
                    //#####################

                    row = 22;
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
                    range.Item[1].Value2 = (traveler.BoxItemCode == "" ? traveler.RegPack : "Use box: " + traveler.BoxItemCode);
                    range.Item[2].Value2 = traveler.RegPackQty;
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
                        // log that this these orders have been printed

                        //foreach (Order order in traveler.Orders)
                        //{
                        //    file.WriteLine(order.SalesOrderNo);
                        //    file.Flush();
                        //}


                        //##### Print the Cover sheet #######
                        outputSheet.PrintOut(
                            Type.Missing, Type.Missing, Type.Missing, Type.Missing,
                            Type.Missing, Type.Missing, Type.Missing, Type.Missing);
                        //###################################

                        // successfully printed, so it should be logged in the printed.json file
                        if (!traveler.Printed)
                        {
                            file.Write(traveler.Export());
                            file.Flush();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("A problem occured when printing: " + ex.Message);
                    }
                }
            }
            file.Close();
            m_infoLabel.Text = "";
        }
        //=======================
        // Properties
        //=======================
        private ListView m_chairListView = null;
        private Label m_infoLabel = null;
        private ProgressBar m_progressBar = null;
        private List<Order> m_orders = new List<Order>();
        private List<Chair> m_travelers = new List<Chair>();
        private OdbcConnection m_MAS = null;

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

        internal List<Chair> Travelers
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
