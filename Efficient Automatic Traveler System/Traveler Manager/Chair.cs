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
    class Chair : Traveler
    {
        //===========================
        // PUBLIC
        //===========================

        // Doesn't do anything
        public Chair(Traveler t, bool copyID = false) : base(t, copyID) { }
        public Chair(Dictionary<string, string> obj) : base(obj) { }
        // Gets the base properties and orders of the traveler from a json string
        public Chair(string json) : base(json)
        {
            GetBlacklist();
        }
        // Creates a traveler from a part number and quantity
        public Chair(string partNo, int quantity) : base(partNo, quantity)
        {
            GetBlacklist();
        }
        // Creates a traveler from a part number and quantity, then loads the bill of materials
        public Chair(string partNo, int quantity, ref OdbcConnection MAS) : base(partNo, quantity, ref MAS)
        {
            GetBlacklist();
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
            if (clientType == "OperatorClient" && m_station == Traveler.GetStation("Chairs"))
            {
                foreach (Item component in m_components)
                {
                    rows += (rows.Length > 0 ? "," : "") + new NameValueQty<string, string>(component.ItemCode, component.ItemCodeDesc, component.TotalQuantity.ToString()).ToString();
                }

            }
            json += rows;
            json += ']';
            json += "}\n";
            return json;
        }
        //===========================
        // Private
        //===========================
        protected override string ExportProperties()
        {
            return ",\"type\":\"Chair\"";
        }
        // returns the next station for this table
        protected override void SetNextStation()
        {
            if (m_station == Traveler.GetStation("Start"))
            {
                m_nextStation = Traveler.GetStation("Chairs");
            }
            else if (m_station == Traveler.GetStation("Chairs"))
            {
                m_nextStation = Traveler.GetStation("Finished");
            }
            else
            {
                m_nextStation = Traveler.GetStation("Start");
            }
        }
        private void GetBlacklist()
        {
            m_blacklist.Add(new BlacklistItem("/")); // Misc work items
        }
    }
}
