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
    class Table : Traveler
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
        public Table(string partNo, int quantity, ref OdbcConnection MAS) : base(partNo,quantity,ref MAS)
        {
            ImportPart(ref MAS);
        }
        // returns a JSON formatted string to be sent to a client
        public override string Export(string clientType)
        {
            string json = "";
            json += "{";
            json += "\"ID\":" + m_ID + ",";
            json += "\"itemCode\":" + '"' + m_part.BillNo + '"' + ",";
            json += "\"quantity\":" + m_quantity + ",";
            json += "\"type\":" + '"' + this.GetType().Name + '"' + ",";
            json += "\"lastStation\":" + '"' + Traveler.GetStationName(m_lastStation) + '"' + ',';
            json += "\"station\":" + '"' + Traveler.GetStationName(m_station) + '"' + ',';
            json += "\"nextStation\":" + '"' + Traveler.GetStationName(m_nextStation) + '"' + ',';
            json += "\"history\":[";
            string rows = "";
            foreach (Event travelerEvent in m_history)
            {
                rows += (rows.Length > 0 ? "," : "") + travelerEvent.ToString();
            }
            json += rows;
            json += "],";
            json += "\"members\":[";
            rows = "";
            rows += (new NameValueQty<string, string>("Description", m_part.BillDesc, "")).ToString();
            if (clientType == "OperatorClient" && m_station ==  Traveler.GetStation("Heian")) {
                rows += (rows.Length > 0 ? "," : "") + new NameValueQty<string, string>("Drawing", m_drawingNo, "").ToString();
                rows += (rows.Length > 0 ? "," : "") + new NameValueQty<string, int>   ("Blank", m_blankSize + " " + m_blankNo, m_blankQuantity).ToString();
                rows += (rows.Length > 0 ? "," : "") + new NameValueQty<string, string>("Material", m_material.ItemCode, m_material.TotalQuantity.ToString() + " " + m_material.Unit.ToString()).ToString();
                rows += (rows.Length > 0 ? "," : "") + new NameValueQty<string, string>("Color", m_color, "").ToString();
            } else if (clientType == "OperatorClient" && m_station == Traveler.GetStation("Vector")) {
                rows += (rows.Length > 0 ? "," : "") + new NameValueQty<string, string>("Drawing", m_drawingNo, "").ToString();
                rows += (rows.Length > 0 ? "," : "") + new NameValueQty<string, string>("Color", m_color, "").ToString();
                rows += (rows.Length > 0 ? "," : "") + new NameValueQty<string, string>("Edgebanding", m_eband.ItemCode, m_eband.TotalQuantity.ToString() + " " + m_eband.Unit).ToString();
            }
            json += rows;
            json += ']';
            json += "}\n";
            return json;
        }
        public override void ImportPart(ref OdbcConnection MAS)
        {
            base.ImportPart(ref MAS);
            m_part.BillDesc = m_part.BillDesc.Replace("TableTopAsm,", ""); // tabletopasm is pretty obvious and therefore extraneous
            m_colorNo = Convert.ToInt32(Part.BillNo.Substring(Part.BillNo.Length - 2));
            m_shapeNo = Part.BillNo.Substring(0, Part.BillNo.Length - 3);
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
        protected int GetNextStation(UInt16 itemID)
        {
            int station = m_items.Find(x => x.ID == itemID).Station;
            if (station == Traveler.GetStation("Start"))
            {
                return Traveler.GetStation("Heian");
            } else if (station == Traveler.GetStation("Heian") || station == Traveler.GetStation("Weeke"))
            {
                // switch between vector and straightline edgebander based on what was in the bill
                if (m_vector != null) {
                     return Traveler.GetStation("Vector");
                } else if (m_ebander != null)
                {
                    return Traveler.GetStation("Edgebander");
                }
               
            } else if (station == Traveler.GetStation("Vector") || station == Traveler.GetStation("Edgebander"))
            {
                return Traveler.GetStation("Table-Pack");
            } else if (station == Traveler.GetStation("Table-Pack"))
            {
                return Traveler.GetStation("Finished");
            } else if (station == Traveler.GetStation("Finished"))
            {
                return Traveler.GetStation("Finished");
            }
            return -1;
        }
        #endregion
        //--------------------------------------------------------
        #region Properties

        // Table
        private int m_colorNo = 0;
        private string m_shapeNo = "";
        private string m_shape = "";
        // Labor
        protected Item m_cnc = null; // labor item
        protected Item m_vector = null; // labor item
        protected Item m_ebander = null; // labor item
        protected Item m_saw = null; // labor item
        protected Item m_assm = null; // labor item
        protected Item m_box = null; // labor item
        // Material
        protected Item m_material = null; // board material
        protected Item m_eband = null; // edgebanding
        protected List<Item> m_components = new List<Item>(); // everything that isn't work, boxes, material or edgebanding
        // Box
        protected int m_partsPerBox = 1;
        protected string m_boxItemCode = "";
        protected string m_regPack = "N/A";
        protected int m_regPackQty = 0;
        protected string m_supPack = "N/A";
        protected int m_supPackQty = 0;
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
        internal string ShapeNo
        {
            get
            {
                return m_shapeNo;
            }

            set
            {
                m_shapeNo = value;
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
        #endregion
        //--------------------------------------------------------
    }
}
