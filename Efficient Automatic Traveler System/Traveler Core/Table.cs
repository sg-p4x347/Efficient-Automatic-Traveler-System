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
        //===========================
        // PUBLIC
        //===========================
        public Table( Table table) : base(table)
        {
            // part information
            m_colorNo = table.ColorNo;
            m_shapeNo = table.ShapeNo;
            m_shape = table.Shape;
            // Blank informatin
            m_blankNo = table.BlankNo;
            m_blankColor = table.BlankColor;
            m_blankSize = table.BlankSize;
            m_partsPerBlank = table.PartsPerBlank;
            m_blankQuantity = table.BlankQuantity;
            m_leftoverParts = table.LeftoverParts;
        }
        public Table() : base() { }
        public Table(string json) : base(json) {
            GetBlacklist();
            m_colorNo = Convert.ToInt32(m_partNo.Substring(m_partNo.Length - 2));
            m_shapeNo = m_partNo.Substring(0, m_partNo.Length - 3);
        }
        // Creates a traveler from a part number and quantity
        public Table(string partNo, int quantity) : base(partNo, quantity)
        {
            GetBlacklist();
            m_colorNo = Convert.ToInt32(m_partNo.Substring(m_partNo.Length - 2));
            m_shapeNo = m_partNo.Substring(0, m_partNo.Length - 3);
        }
        public Table(string partNo, int quantity, OdbcConnection MAS) : base(partNo,quantity,MAS)
        {
            GetBlacklist();
            m_colorNo = Convert.ToInt32(m_partNo.Substring(m_partNo.Length - 2));
            m_shapeNo = m_partNo.Substring(0, m_partNo.Length - 3);
        }
        // returns a JSON formatted string to be sent to a client
        public string Export(string clientType)
        {
            string json = "";
            json += "{";
            json += "\"ID\":" + '"' + m_ID.ToString("D6") + '"' + ",";
            json += "\"itemCode\":" + '"' + m_part.BillNo + '"' + ",";
            json += "\"quantity\":" + '"' + m_quantity + '"' + ",";
            json += "\"type\":" + '"' + this.GetType().Name + '"' + ",";
            json += "\"station\":" + '"' + Traveler.GetStationName(m_station) + '"' + ',';
            json += "\"nextStation\":" + '"' + Traveler.GetStationName(m_nextStation) + '"' + ',';
            json += "\"members\":[";
            string rows = "";
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

        //--------------------------
        // Private members
        //--------------------------

        private void GetBlacklist()
        {
            m_blacklist.Add(new BlacklistItem("88")); // Glue items
            m_blacklist.Add(new BlacklistItem("92")); // Foam items
            m_blacklist.Add(new BlacklistItem("/")); // Misc work items
        }
        // returns the next station for this table
        protected override void SetNextStation()
        {
            if (m_station == Traveler.GetStation("Start"))
            {
                m_nextStation = Traveler.GetStation("Heian");
            } else if (m_station == Traveler.GetStation("Heian") || m_station == Traveler.GetStation("Weeke"))
            {
                // switch between vector and straightline edgebander based on what was in the bill
                if (m_vector != null) {
                    m_nextStation = Traveler.GetStation("Vector");
                } else if (m_ebander != null)
                {
                    m_nextStation = Traveler.GetStation("Edgebander");
                }
               
            } else if (m_station == Traveler.GetStation("Vector") || m_station == Traveler.GetStation("Edgebander"))
            {
                m_nextStation = Traveler.GetStation("Table-Pack");
            } else if (m_station == Traveler.GetStation("Table-Pack"))
            {
                m_nextStation = Traveler.GetStation("Finished");
            } else if (m_station == Traveler.GetStation("Finished"))
            {
                m_nextStation = Traveler.GetStation("Finished");
            } else
            {
                m_nextStation = Traveler.GetStation("Start");
            }
        }
        
        //--------------------------
        // Properties
        //--------------------------

        // part information
        private int m_colorNo = 0;
        private string m_shapeNo = "";
        private string m_shape = "";
        // Blank information
        private string m_blankNo = "";
        private string m_blankColor = "";
        private string m_blankSize = "";
        private int m_partsPerBlank = 0;
        private int m_blankQuantity = 0;
        private int m_leftoverParts = 0;

        public int ColorNo
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

        public string ShapeNo
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

        public string BlankNo
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

        public string BlankSize
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

        public int PartsPerBlank
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

        public int BlankQuantity
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

        public int LeftoverParts
        {
            get
            {
                return m_leftoverParts;
            }

            set
            {
                m_leftoverParts = value;
            }
        }

        public string BlankColor
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

        public string Shape
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
    }
}
