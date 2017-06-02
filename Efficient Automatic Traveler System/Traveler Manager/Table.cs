using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data.Odbc;
//using Excel = Microsoft.Office.Interop.Excel;
//using Marshal = System.Runtime.InteropServices.Marshal;
using System.Text.RegularExpressions;

namespace Efficient_Automatic_Traveler_System
{
    internal class Table : Traveler, IPart
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
        public Table() : base() {
            m_part = null;
        }
        public Table(Form form) : base(form)
        {
            m_part = new Bill(form.ValueOf("itemCode"), 1, Convert.ToInt32(form.ValueOf("quantity")));
        }
        public Table(string json) : base(json) {
            Dictionary<string, string> obj = new StringStream(json).ParseJSON();
            if (obj["itemCode"] != "")
            {
                m_part = new Bill(obj["itemCode"], 1, m_quantity);
            }
        }
        // create a Table from partNo, quantity, and a MAS connection
        public Table(string itemCode, int quantity) : base(quantity) {
            m_part = new Bill(itemCode, 1, quantity);
        }
        public override bool CombinesWith(object[] args)
        {
            return ItemCode == (string)args[0];
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new StringStream(base.ToString()).ParseJSON(false);
            obj.Add("itemCode", (m_part != null ? m_part.BillNo : "").Quotate());
            return obj.Stringify();
        }
        // returns a JSON formatted string to be sent to a client
        //public override string Export(string clientType, StationClass station)
        //{
        //    Dictionary<string, string> obj = new StringStream(base.Export(clientType, station)).ParseJSON(false);
        //    obj.Add("itemCode", ItemCode);
        //    return obj.Stringify();
        //}
        public override string ExportTableRows(string clientType, StationClass station)
        {
            string json = "{\"members\":[";
            if (clientType == "OperatorClient" && (station.Type == "heian" || station.Type == "weeke")) {
                json += new NameValueQty<string, int>("Part", ItemCode, Quantity).ToString();
                json += ',' + new NameValueQty<string, string>("Description", m_part.BillDesc, "").ToString();
                json += ',' + new NameValueQty<string, string>("Drawing", m_part.DrawingNo, "").ToString();
                json += ',' + new NameValueQty<string, int>   ("Blank", m_blankSize + " " + m_blankNo, m_blankQuantity).ToString();
                //rows += (rows.Length > 0 ? "," : "") + new NameValueQty<string, string>("Material", m_material.ItemCode, m_material.TotalQuantity.ToString() + " " + m_material.Unit.ToString()).ToString();
                json += ',' + new NameValueQty<string, string>("Color", m_color, "").ToString();
                json += ',' + new NameValueQty<string, string>("Rate", GetRate(Part.ComponentBills[0], station).ToString() + " min","").ToString();
                if (Comment != "") json += ',' + new NameValueQty<string, string>("Comment", Comment, "").ToString();
            } else if (clientType == "OperatorClient" && station == StationClass.GetStation("Vector")) {
                json += new NameValueQty<string, string>("Drawing", m_part.DrawingNo, "").ToString();
                json += ',' + new NameValueQty<string, string>("Color", m_color, "").ToString();
                json += ',' + new NameValueQty<string, string>("Edgebanding", BandingColor, "").ToString();
                if (Comment != "") json += ',' + new NameValueQty<string, string>("Comment", Comment, "").ToString();
            }
            
            return json + "]}";
        }
        public override string ExportHuman()
        {
            Dictionary<string, string> obj = new StringStream(base.ExportHuman()).ParseJSON(false);
            obj.Add("Model", (m_part != null ? m_part.BillNo : "").Quotate());
            obj.Add("Description", (m_part != null ? m_part.BillDesc : "").Quotate());
            return obj.Stringify();
        }
        public new static string ExportCSVheader()
        {
            List<string> header = new StringStream('[' + Traveler.ExportCSVheader() + ']').ParseJSONarray(false);
            header.Add("Part");
            header.Add("Description");
            header.Add("Color");
            header.Add("Banding Color");
            header.Add("Blank");
            header.Add("Blank Size");
            header.Add("Blank Qty");
            header.Add("Labor");
            return header.Stringify<string>(false).Trim('[').Trim(']');
        }
        public override string ExportCSVdetail()
        {
            List<string> detail = new StringStream('[' + base.ExportCSVdetail() + ']').ParseJSONarray(false);
            detail.Add(ItemCode.Quotate());
            detail.Add(Part.BillDesc.Quotate());
            detail.Add(m_color.Quotate());
            detail.Add(m_bandingColor.Quotate());
            detail.Add(m_blankNo.Quotate());
            detail.Add(m_blankSize.Quotate());
            detail.Add(m_blankQuantity.ToString());
            detail.Add(GetTotalLabor().ToString());
            return detail.Stringify<string>(false).Trim('[').Trim(']');
        }
        public override Dictionary<string, string> ExportProperties(StationClass station)
        {
            Dictionary<string, string> obj = new Dictionary<string, string>();
            try
            {
                obj.Merge(base.ExportProperties(station));
                obj.Add("shape", Shape.Quotate());
                obj.Add("laborRate", GetRate(Part.ComponentBills[0], station).ToString());
                obj.Add("totalLabor", Math.Round(GetTotalLabor(station), 1).ToString());
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
            }
            return obj;
        }
        // export for summary view
        public override string ExportSummary()
        {
            // Displays properties in order
            Dictionary<string, string> obj = new StringStream(base.ExportSummary()).ParseJSON(false);
            obj.Add("Model", m_part.BillNo.Quotate());
            return obj.Stringify();
        }
        public async override Task ImportInfo(ITravelerManager travelerManager, IOrderManager orderManager, OdbcConnection MAS)
        {
            m_part = new Bill(m_part.BillNo, m_part.QuantityPerBill, Quantity);
            await m_part.Import(MAS);
            m_part.BillDesc = Regex.Replace(m_part.BillDesc,"TableTopAsm,", "", RegexOptions.IgnoreCase); // tabletopasm is pretty obvious and therefore extraneous
            m_part.BillDesc = Regex.Replace(m_part.BillDesc, "TableTop,", "", RegexOptions.IgnoreCase);
            m_colorNo = Convert.ToInt32(Part.BillNo.Substring(Part.BillNo.Length - 2));
            // Table info in the table csv
            GetColorInfo();
            GetBlankInfo();
            GetPackInfo(orderManager);
            // for work rates
            //FindComponents(m_part);
        }
        public override void AdvanceItem(ushort ID, ITravelerManager travelerManager = null)
        {
            TravelerItem item = FindItem(ID);
            item.Station = GetNextStation(ID);
            // Queue box after table leaves vector
            if (travelerManager != null && item.Station.Type == "tablePack" && ChildTravelers.Count == 0)
            {
                TableBox box = CreateBoxTraveler();
                box.Quantity = Quantity;
                box.EnterProduction(travelerManager);
                travelerManager.GetTravelers.Add(box);
            }
        }
        public override void FinishItem(ushort ID)
        {
            base.FinishItem(ID);
            TravelerItem item = FindItem(ID);

            // add this item to inventory
            InventoryManager.Add(ItemCode);
        }
        // labels
        public override string GetLabelFields(ushort itemID, LabelType type)
        {
            TravelerItem item = FindItem(itemID);
            string json = "";
            switch (type)
            {
                case LabelType.Tracking:
                    json += ",\"Barcode\":" + '"' + ID.ToString("D6") + '-' + itemID.ToString("D4") + '"'; // 11 digits [000000]-[0000]
                    // Item ID is now a sequence number out of the qty on the traveler
                    json += ",\"ID\":\"" + ID.ToString("D6") + '-' + PrintSequenceNo(item) + "\"";
                    json += ",\"Desc1\":\"" + Part.BillNo + "\"";
                    json += ",\"Desc2\":\"" + Part.BillDesc + "\"";
                    json += ",\"Desc3\":\"" + m_bandingAbrev + "\"";
                    break;
                case LabelType.Scrap:
                    json += ",\"Barcode\":" + '"' + ID.ToString("D6") + '-' + itemID.ToString("D4") + '"'; // 11 digits [000000]-[0000]
                    json += ",\"ID\":\"" + ID.ToString("D6") + '-' + PrintSequenceNo(item) + '/' + Quantity.ToString() + "\"";
                    json += ",\"Desc1\":\"" + Part.BillNo + "\"";
                    json += ",\"Desc2\":\"" + "!! " + PrintSequenceNo(item) +  " !!\"";
                    ScrapEvent scrapEvent = FindItem(itemID).History.OfType<ScrapEvent>().ToList().Find(x => x.Process == ProcessType.Scrapped);
                    string reason = scrapEvent.Reason;
                    json += ",\"Reason\":" + reason.Quotate();
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

        
        // Gets the work rate for the current station
        public override double GetCurrentLabor(StationClass station)
        {
            // gets the rate from the first (and only) bill; this is the common bill that all tables share
            return GetRate(Part.ComponentBills[0], station != null ? station : Station);
        }
        public override double GetTotalLabor(StationClass station)
        {
            if (station != null)
            {
                return GetRate(Part.ComponentBills[0], station, true);
            } else
            {
                // sum up every station
                return StationClass.GetStations().Sum(i => GetTotalLabor(i));
            }
        }
        public double GetTotalLabor()
        {
            double total = 0.0;
            foreach (string stationName in StationClass.StationNames())
            {
                total += GetTotalLabor(StationClass.GetStation(stationName));
            }
            return total;
        }
        // Create a box traveler
        public TableBox CreateBoxTraveler()
        {
            TableBox box = new TableBox(this);
            ChildTravelers.Add(box);
            return box;
        }
        public override void EnterProduction(ITravelerManager travelerManager)
        {
            base.EnterProduction(travelerManager);
        }
        public override string PrintLabel(ushort itemID, LabelType type, int qty = 1, bool forcePrint = false)
        {
            return base.PrintLabel(itemID, type, type == LabelType.Pack ? m_packLabelQty : qty, forcePrint);
        }

        // IForm -------------------
        public override Form CreateForm()
        {
            Form form = base.CreateForm();
            form.Title = "Table";
            form.Textbox("itemCode", "Model");
            return form;
        }

        public override Form CreateFilledForm()
        {
            Form form = base.CreateFilledForm();
            form.Title = "Edit Table";
            form.Textbox("itemCode", "Model",ItemCode);
            return form;
        }
        //-------------------------

        #endregion
        //--------------------------------------------------------
        #region Private Methods
        
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
            // read past the header
            List<string> header = colorRef.ReadLine().Split(',').ToList();
            string line = colorRef.ReadLine();
            while (line != "" && line != null)
            {
                string[] row = line.Split(',');
                if (Convert.ToInt32(row[header.IndexOf("Color #")]) == ColorNo)
                {
                    m_color = row[header.IndexOf("Color")];
                    m_bandingColor = row[header.IndexOf("Banding Color")];
                    BlankColor = row[header.IndexOf("Blank Code")];
                    m_bandingAbrev = row[header.IndexOf("Banding Abreviation")];
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
            // read past the header
            List<string> header = tableRef.ReadLine().Split(',').ToList();
            string line = tableRef.ReadLine();
            while (line != "" && line != null)
            {
                string[] row = line.Split(',');
                if (Part.BillNo.Contains(row[header.IndexOf("Table")]))
                {
                    Size = row[header.IndexOf("Size")];
                    Shape = row[header.IndexOf("Shape Type")];
                    PalletSize = row[header.IndexOf("Pallet")];
                    //--------------------------------------------
                    // BLANK INFO
                    //--------------------------------------------

                    BlankSize = row[header.IndexOf("Blank Size")];
                    SheetSize = row[header.IndexOf("Sheet Size")];
                    // [column 3 contains # of blanks per sheet]
                    PartsPerBlank = row[header.IndexOf("Tables Per Blank")] != "" ? Convert.ToInt32(row[header.IndexOf("Tables Per Blank")]) : 0;

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
                    if (BlankColor == "MAGR" && row[header.IndexOf("MAGR blank")] != "")
                    {
                        BlankNo = row[header.IndexOf("MAGR blank")];
                    }
                    // check to see if there is a CHOK blank
                    else if (BlankColor == "CHOK" && row[header.IndexOf("CHOK blank")] != "")
                    {
                        BlankNo = row[header.IndexOf("CHOK blank")];
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

                    //--------------------------------------------
                    // Pack Info
                    //--------------------------------------------
                    string boxType = row[header.IndexOf("Box Type")];
                    if (boxType == "TD")
                    {
                        m_boxPieceQty = 2;
                    } else if (boxType == "FPF")
                    {
                        m_boxPieceQty = 1;
                    }
                    if (Convert.ToBoolean(row[header.IndexOf("2PerTopBottom")].ToLower()))
                    {
                        m_boxPieceQty = 4;
                    }
                    //--------------------------------------------
                    // PALLET
                    //--------------------------------------------
                    PalletSize = row[11];
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
                    foreach (string orderNo in ParentOrderNums)
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
        protected Bill m_part;
        private int m_colorNo = 0;
        private string m_color = "";
        private string m_bandingColor = "";
        private string m_bandingAbrev = "";
        private string m_shape = "";
        private string m_size = "";
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
        private int m_packLabelQty = 2;
        private int m_boxPieceQty = 1;
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
        public Bill Part
        {
            get
            {
                return m_part;
            }
        }
        public string ItemCode
        {
            get
            {
                return m_part != null ? m_part.BillNo : "";
            }
        }
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

        public string Size
        {
            get
            {
                return m_size;
            }

            set
            {
                m_size = value;
            }
        }

        public int PackLabelQty
        {
            get
            {
                return m_packLabelQty;
            }

            set
            {
                m_packLabelQty = value;
            }
        }

        public string Color
        {
            get
            {
                return m_color;
            }

            set
            {
                m_color = value;
            }
        }
        #endregion
        //--------------------------------------------------------
    }
}
