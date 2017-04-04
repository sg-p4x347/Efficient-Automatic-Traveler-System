using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data.Odbc;
using Excel = Microsoft.Office.Interop.Excel;
using Marshal = System.Runtime.InteropServices.Marshal;

namespace Efficient_Automatic_Traveler_System
{
    internal class Table : Traveler
    {
        #region Public Methods
        //--------------------------
        // Public members
        //--------------------------
        //public Table(Traveler t, bool copyID = false) : base(t,copyID) {
        //    GetBlacklist();
        //    m_colorNo = Convert.ToInt32(m_itemCode.Substring(m_itemCode.Length - 2));
        //    m_shapeNo = m_itemCode.Substring(0, m_itemCode.Length - 3);
        //}
        //public Table(Dictionary<string,string> obj) : base(obj)
        //{
        //    GetBlacklist();
        //    m_colorNo = Convert.ToInt32(m_itemCode.Substring(m_itemCode.Length - 2));
        //    m_shapeNo = m_itemCode.Substring(0, m_itemCode.Length - 3);
        //}
        //public Table(Table table) : base((Traveler) table)
        //{
        //    // part information
        //    m_colorNo = table.ColorNo;
        //    m_shapeNo = table.ShapeNo;
        //    m_shape = table.Shape;
        //    // Blank informatin
        //    m_blankNo = table.BlankNo;
        //    m_blankColor = table.BlankColor;
        //    m_blankSize = table.BlankSize;
        //    m_partsPerBlank = table.PartsPerBlank;
        //    m_blankQuantity = table.BlankQuantity;
        //    m_leftoverParts = table.LeftoverParts;
        //}
        //public override Traveler Clone()
        //{
        //    Table t = new Table(this);
        //    m_children.Add(t.ID);
        //    t.Parents.Add(m_ID);
        //    return t;
        //}
        public Table() : base() { }
        public Table(string json) : base(json) {

        }
        // create a Table from partNo, quantity, and a MAS connection
        public Table(string partNo, int quantity) : base(partNo, quantity) { }
        // returns a JSON formatted string to be sent to a client
        public override string ExportTableRows(string clientType, StationClass station)
        {
            string json = "";
            if (clientType == "OperatorClient" && station == StationClass.GetStation("Heian1") || station == StationClass.GetStation("Heian2")) {
                json += ',' + new NameValueQty<string, string>("Drawing", m_part.DrawingNo, "").ToString();
                json += ',' + new NameValueQty<string, int>   ("Blank", m_blankSize + " " + m_blankNo, m_blankQuantity).ToString();
                //rows += (rows.Length > 0 ? "," : "") + new NameValueQty<string, string>("Material", m_material.ItemCode, m_material.TotalQuantity.ToString() + " " + m_material.Unit.ToString()).ToString();
                json += ',' + new NameValueQty<string, string>("Color", m_color, "").ToString();
            } else if (clientType == "OperatorClient" && station == StationClass.GetStation("Vector")) {
                json += ',' + new NameValueQty<string, string>("Drawing", m_part.DrawingNo, "").ToString();
                json += ',' + new NameValueQty<string, string>("Color", m_color, "").ToString();
                //rows += (rows.Length > 0 ? "," : "") + new NameValueQty<string, string>("Edgebanding", m_eband.ItemCode, m_eband.TotalQuantity.ToString() + " " + m_eband.Unit).ToString();
            }
            return json;
        }
        public override void ImportPart(IOrderManager orderManager, ref OdbcConnection MAS)
        {
            base.ImportPart(orderManager, ref MAS);
            m_part.BillDesc = m_part.BillDesc.Replace("TableTopAsm,", ""); // tabletopasm is pretty obvious and therefore extraneous
            m_colorNo = Convert.ToInt32(Part.BillNo.Substring(Part.BillNo.Length - 2));
            // Table info in the table csv
            GetColorInfo();
            GetBlankInfo();
            GetPackInfo(orderManager);
            // for work rates
            //FindComponents(m_part);
        }
        public override void AdvanceItem(ushort ID)
        {
            FindItem(ID).Station = GetNextStation(ID);
        }
        // labels
        public override string GetLabelFields(ushort itemID, LabelType type)
        {
            string json = "";
            switch (type)
            {
                case LabelType.Tracking:
                    json += ",\"Barcode\":" + '"' + ID.ToString("D6") + '-' + itemID.ToString("D4") + '"'; // 11 digits [000000]-[0000]
                    json += ",\"ID\":\"" + ID.ToString("D6") + '-' + itemID + "\"";
                    json += ",\"Desc1\":\"" + Part.BillNo + "\"";
                    json += ",\"Desc2\":\"" + Part.BillDesc + "\"";
                    break;
                case LabelType.Scrap:
                    json += ",\"Barcode\":" + '"' + ID.ToString("D6") + '-' + itemID.ToString("D4") + '"'; // 11 digits [000000]-[0000]
                    json += ",\"ID\":\"" + ID.ToString("D6") + '-' + itemID + "\"";
                    json += ",\"Desc1\":\"" + Part.BillNo + "\"";
                    json += ",\"Desc2\":\"" + "!!!***SCRAP***!!!" + "\"";
                    break;
                case LabelType.Table:
                    json += GetLabelFields(new List<string>()
                    {
                        "Marco Item #",
                        "ProdLine",
                        "DescriptionShort",
                        "Color1",
                        "Color2",
                        "Origin1"
                    });
                    break;
                case LabelType.Pack:
                    json += ",\"Barcode\":" + '"' + ID.ToString("D6") + '-' + itemID.ToString("D4") + '"'; // 11 digits [000000]-[0000]
                    json += ",\"Order#\":\"" + (FindItem(itemID).Order != "" ? "Order: " + FindItem(itemID).Order : "To inventory") + "\"";

                    json += GetLabelFields(new List<string>()
                    {
                        "Marco Item #",
                        "ProdLine",
                        "DescriptionShort",
                        "Color1",
                        "Color2"
                    });
                    break;
            }
            return json;
        }
        public override StationClass GetNextStation(UInt16 itemID)
        {
            StationClass station = Items.Find(x => x.ID == itemID).Station;
            if (station == StationClass.GetStation("Start"))
            {
                return StationClass.GetStation("Start");
            }
            else if (station.Name.Contains("Heian") || station.Name.Contains("Weeke"))
            {
                // switch between vector and straightline edgebander based on what was in the bill
                if (m_ebander != null)
                {
                    return StationClass.GetStation("Edgebander");

                }
                else
                {
                    return StationClass.GetStation("Vector");
                }

            }
            else if (station == StationClass.GetStation("Vector") || station == StationClass.GetStation("Edgebander"))
            {
                return StationClass.GetStation("Table-Pack");
            }
            else if (station == StationClass.GetStation("Table-Pack"))
            {
                return StationClass.GetStation("Finished");
            }
            else if (station == StationClass.GetStation("Finished"))
            {
                return StationClass.GetStation("Finished");
            } else
            {
                return station;
            }
        }

        public new static string ExportCSVheader()
        {
            List<string> header = new List<string>();
            header.Add("Color");
            header.Add("Banding Color");
            header.Add("Blank");
            header.Add("Blank Size");
            header.Add("Blank Qty");
            return Traveler.ExportCSVheader() + ',' + header.Stringify<string>(false).Trim('[').Trim(']');
        }
        public override string ExportCSVdetail()
        {
            List<string> detail = new List<string>();
            detail.Add(m_color.Quotate());
            detail.Add(m_bandingColor.Quotate());
            detail.Add(m_blankNo.Quotate());
            detail.Add(m_blankSize.Quotate());
            detail.Add(m_blankQuantity.ToString());
            return base.ExportCSVdetail() + ',' + detail.Stringify<string>(false).Trim('[').Trim(']');
        }
        // Gets the work rate for the current station
        public override double GetCurrentRate()
        {
            // gets the rate from the first (and only) bill; this is the common bill that all tables share
            return GetRate(Part.ComponentBills[0], Station);
        }
        #endregion
        //--------------------------------------------------------
        #region Private Methods
        protected override string ExportProperties()
        {
            return ",\"type\":\"Table\"";
        }
        //private void GetBlacklist()
        //{
        //    m_blacklist.Add(new BlacklistItem("88")); // Glue items
        //    m_blacklist.Add(new BlacklistItem("92")); // Foam items
        //    m_blacklist.Add(new BlacklistItem("/")); // Misc work items
        //}
        // returns the next station for this table
        
        private void GetColorInfo()
        {
            // open the color ref csv file
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            System.IO.StreamReader colorRef = new StreamReader(System.IO.Path.Combine(exeDir, "Color Reference.csv"));
            colorRef.ReadLine(); // read past the header
            string line = colorRef.ReadLine();
            while (line != "" && line != null)
            {
                string[] row = line.Split(',');
                if (Convert.ToInt32(row[0]) == ColorNo)
                {
                    m_color = row[1];
                    m_bandingColor = row[2];
                    BlankColor = row[3];
                    break;
                }
                line = colorRef.ReadLine();
            }
            colorRef.Close();
        }
        // calculate how many actual tables will be produced from the blanks
        private void GetBlankInfo()
        {
            // open the table ref csv file
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            System.IO.StreamReader tableRef = new StreamReader(System.IO.Path.Combine(exeDir, "Table Reference.csv"));
            tableRef.ReadLine(); // read past the header
            string line = tableRef.ReadLine();
            while (line != "" && line != null)
            {
                string[] row = line.Split(',');
                if (Part.BillNo.Contains(row[0]))
                {
                    //--------------------------------------------
                    // BLANK INFO
                    //--------------------------------------------

                    BlankSize = row[2];
                    SheetSize = row[3];
                    // [column 3 contains # of blanks per sheet]
                    PartsPerBlank = row[5] != "" ? Convert.ToInt32(row[5]) : 0;

                    // Exception cases -!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!
                    List<int> exceptionColors = new List<int> { 60, 50, 49 };
                    if ((Part.BillNo.Contains("MG2247") || Part.BillNo.Contains("38-2247")) && exceptionColors.IndexOf(ColorNo) != -1)
                    {
                        // Exceptions to the blank parent sheet (certain colors have grain that can't be used with the typical blank)
                        BlankComment = "Use " + SheetSize + " sheet and align grain";
                        PartsPerBlank = 2;
                    }
                    //!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!-!

                    // check to see if there is a MAGR blank
                    if (BlankColor == "MAGR" && row[6] != "")
                    {
                        BlankNo = row[6];
                    }
                    // check to see if there is a CHOK blank
                    else if (BlankColor == "CHOK" && row[7] != "")
                    {
                        BlankNo = row[7];
                    }
                    // there are is no specific blank size in the kanban
                    else
                    {
                        BlankNo = "";
                    }
                    // calculate production numbers
                    if (PartsPerBlank <= 0) PartsPerBlank = 1;
                    decimal tablesPerBlank = Convert.ToDecimal(PartsPerBlank);
                    BlankQuantity = Convert.ToInt32(Math.Ceiling(Convert.ToDecimal(Quantity) / tablesPerBlank));
                    //int partsProduced = BlankQuantity * Convert.ToInt32(tablesPerBlank);
                    //LeftoverParts = partsProduced - Quantity;
                }
                line = tableRef.ReadLine();
            }
            tableRef.Close();
        }
        // calculate how much of each box size
        private void GetPackInfo(IOrderManager orderManager)
        {
            // open the table ref csv file
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            System.IO.StreamReader tableRef = new StreamReader(System.IO.Path.Combine(exeDir, "Table Reference.csv"));
            tableRef.ReadLine(); // read past the header
            string line = tableRef.ReadLine();
            while (line != "" && line != null)
            {
                string[] row = line.Split(',');
                if (Part.BillNo.Contains(row[0]))
                {
                    //--------------------------------------------
                    // PACK & BOX INFO
                    //--------------------------------------------
                    SupPack = row[8];
                    RegPack = row[9];
                    foreach (string orderNo in ParentOrders)
                    {
                        Order order = orderManager.FindOrder(orderNo);
                        foreach (OrderItem orderItem in order.FindItems(ID))
                        {
                            // TEMP
                            //// Get box information
                            //if (order.ShipVia != "" && (order.ShipVia.ToUpper().IndexOf("FEDEX") != -1 || order.ShipVia.ToUpper().IndexOf("UPS") != -1))
                            //{
                            //    SupPackQty += orderItem.QtyOrdered;
                            //}
                            //else
                            //{
                            //    RegPackQty += orderItem.QtyOrdered;
                            //    // approximately 20 max tables per pallet
                            //    PalletQty += Convert.ToInt32(Math.Ceiling(Convert.ToDouble(orderItem.QtyOrdered) / 20));
                            //}
                        }
                    }
                    //--------------------------------------------
                    // PALLET
                    //--------------------------------------------
                    PalletSize = row[11];
                    break;
                }
                line = tableRef.ReadLine();
            }
            tableRef.Close();
        }
        // get a list of fields from the label DB
        private string GetLabelFields(List<string> fieldNames)
        {
            string json = "";
            // open the pack label database
            System.IO.StreamReader labelRef = new StreamReader(@"\\MGFS01\ZebraPrinter\data\databases\production.csv");
            string[] headerArray = labelRef.ReadLine().Split(',');

            string line = labelRef.ReadLine();
            while (line != "" && line != null)
            {
                string[] rowArray = line.Split(',');
                if (Part.BillNo.Contains(rowArray[0]))
                {
                    for (int index = 0; index < headerArray.Count(); index++)
                    {
                        if (fieldNames.Contains(headerArray[index]))
                        {
                            json += ',' + headerArray[index].Quotate() + ':' + rowArray[index].Quotate();
                        }
                    }
                    break;
                }

                line = labelRef.ReadLine();
            }
            return json;
        }
        // Finds all the components in the top level bill, setting key components along the way
        public void FindComponents(Bill bill)
        {
            // find work and or material
            foreach (Item componentItem in bill.ComponentItems)
            {
                // update the component's total quantity
                componentItem.TotalQuantity = bill.TotalQuantity * componentItem.QuantityPerBill;
                // sort out key components
                string itemCode = componentItem.ItemCode;
                if (itemCode == "/LWKE1" || itemCode == "/LWKE2" || itemCode == "/LCNC1" || itemCode == "/LCNC2")
                {
                    // CNC labor
                    if (m_cnc == null)
                    {
                        m_cnc = componentItem;
                    }
                    else
                    {
                        m_cnc.TotalQuantity += componentItem.TotalQuantity;
                    }
                }
                else if (itemCode == "/LBND2" || itemCode == "/LBND3")
                {
                    // Straight Edgebander labor
                    if (m_ebander == null)
                    {
                        m_ebander = componentItem;
                    }
                    else
                    {
                        m_ebander.TotalQuantity += componentItem.TotalQuantity;
                    }
                }
                else if (itemCode == "/LPNL1" || itemCode == "/LPNL2")
                {
                    // Panel Saw labor
                    if (m_saw == null)
                    {
                        m_saw = componentItem;
                    }
                    else
                    {
                        m_saw.TotalQuantity += componentItem.TotalQuantity;
                    }
                }
                else if (itemCode == "/LCEB1" | itemCode == "/LCEB2")
                {
                    // Contour Edge Bander labor (vector)
                    if (m_vector == null)
                    {
                        m_vector = componentItem;
                    }
                    else
                    {
                        m_vector.TotalQuantity += componentItem.TotalQuantity;
                    }
                }
                else if (itemCode == "/LATB1" || itemCode == "/LATB2" || itemCode == "/LATB3" || itemCode == "/LACH1" || itemCode == "/LACH2" || itemCode == "/LACH3")
                {
                    // Assembly labor
                    if (m_assm == null)
                    {
                        m_assm = componentItem;
                    }
                    else
                    {
                        m_assm.TotalQuantity += componentItem.TotalQuantity;
                    }
                }
                else if (itemCode == "/LBOX1")
                {
                    // Box construction labor
                    if (m_box == null)
                    {
                        m_box = componentItem;
                    }
                    else
                    {
                        m_box.TotalQuantity += componentItem.TotalQuantity;
                    }
                }
                else if (itemCode.Substring(0, 3) == "006")
                {
                    // Material
                    if (m_material == null)
                    {
                        m_material = componentItem;
                    }
                    else
                    {
                        m_material.TotalQuantity += componentItem.TotalQuantity;
                    }
                }
                else if (itemCode.Substring(0, 2) == "87")
                {
                    // Edgeband
                    if (m_eband == null)
                    {
                        m_eband = componentItem;
                    }
                    else
                    {
                        m_eband.TotalQuantity += componentItem.TotalQuantity;
                    }
                }
                else if (m_box == null && itemCode.Substring(0, 2) == "90")
                {
                    // Paid for box
                    m_boxItemCode = itemCode;
                }
                else
                {
                    // anything else
                    // check the blacklist
                    bool blacklisted = false;
                    // TEMP
                    //foreach (BlacklistItem blItem in m_blacklist)
                    //{
                    //    if (blItem.StartsWith(itemCode))
                    //    {
                    //        blacklisted = true;
                    //        break;
                    //    }
                    //}
                    if (!blacklisted)
                    {
                        // check for existing item first
                        bool foundItem = false;
                        foreach (Item component in m_components)
                        {
                            if (component.ItemCode == itemCode)
                            {
                                foundItem = true;
                                component.TotalQuantity += componentItem.TotalQuantity;
                                break;
                            }
                        }
                        if (!foundItem)
                        {
                            m_components.Add(componentItem);
                        }
                    }
                }
            }
            // Go deeper into each component bill
            foreach (Bill componentBill in bill.ComponentBills)
            {
                //componentBill.TotalQuantity = bill.TotalQuantity * componentBill.QuantityPerBill;
                FindComponents(componentBill);
            }
        }
        #endregion
        //--------------------------------------------------------
        #region Properties

        // Table
        private int m_colorNo = 0;
        private string m_color = "";
        private string m_bandingColor = "";
        private string m_shape = "";
        // Labor
        private Item m_cnc = null; // labor item
        private Item m_vector = null; // labor item
        private Item m_ebander = null; // labor item
        private Item m_saw = null; // labor item
        private Item m_assm = null; // labor item
        private Item m_box = null; // labor item
        // Material
        private Item m_material = null; // board material
        private Item m_eband = null; // edgebanding
        private List<Item> m_components = new List<Item>(); // everything that isn't work, boxes, material or edgebanding
        // Box
        private string m_boxItemCode = "";
        private string m_regPack = "N/A";
        private int m_regPackQty = 0;
        private string m_supPack = "N/A";
        private int m_supPackQty = 0;
        // Blank
        private string m_sheetSize = "";
        private string m_blankNo = "";
        private string m_blankColor = "";
        private string m_blankSize = "";
        private string m_blankComment = "";
        private int m_partsPerBlank = 0;
        private int m_blankQuantity = 0;
        // Pallet
        private string m_palletSize = "";
        private int m_palletQty = 0;

        #endregion
        //--------------------------------------------------------
        #region Interface
        internal int ColorNo
        {
            get
            {
                return m_colorNo;
            }

            set
            {
                m_colorNo = value;
            }
        }
        internal string Shape
        {
            get
            {
                return m_shape;
            }

            set
            {
                m_shape = value;
            }
        }
        internal string BlankNo
        {
            get
            {
                return m_blankNo;
            }

            set
            {
                m_blankNo = value;
            }
        }
        internal string BlankSize
        {
            get
            {
                return m_blankSize;
            }

            set
            {
                m_blankSize = value;
            }
        }
        internal int PartsPerBlank
        {
            get
            {
                return m_partsPerBlank;
            }

            set
            {
                m_partsPerBlank = value;
            }
        }
        internal int BlankQuantity
        {
            get
            {
                return m_blankQuantity;
            }

            set
            {
                m_blankQuantity = value;
            }
        }
        internal string BlankColor
        {
            get
            {
                return m_blankColor;
            }

            set
            {
                m_blankColor = value;
            }
        }
        internal string SheetSize
        {
            get
            {
                return m_sheetSize;
            }

            set
            {
                m_sheetSize = value;
            }
        }
        internal string BlankComment
        {
            get
            {
                return m_blankComment;
            }

            set
            {
                m_blankComment = value;
            }
        }
        internal string PalletSize
        {
            get
            {
                return m_palletSize;
            }

            set
            {
                m_palletSize = value;
            }
        }
        internal int PalletQty
        {
            get
            {
                return m_palletQty;
            }

            set
            {
                m_palletQty = value;
            }
        }
        internal string BoxItemCode
        {
            get
            {
                return m_boxItemCode;
            }

            set
            {
                m_boxItemCode = value;
            }
        }
        internal string RegPack
        {
            get
            {
                return m_regPack;
            }

            set
            {
                m_regPack = value;
            }
        }
        internal int RegPackQty
        {
            get
            {
                return m_regPackQty;
            }

            set
            {
                m_regPackQty = value;
            }
        }
        internal string SupPack
        {
            get
            {
                return m_supPack;
            }

            set
            {
                m_supPack = value;
            }
        }
        internal int SupPackQty
        {
            get
            {
                return m_supPackQty;
            }

            set
            {
                m_supPackQty = value;
            }
        }

        internal string BandingColor
        {
            get
            {
                return m_bandingColor;
            }

            set
            {
                m_bandingColor = value;
            }
        }
        #endregion
        //--------------------------------------------------------
    }
}
