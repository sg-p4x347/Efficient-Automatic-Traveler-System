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
    internal class Chair : Traveler
    {
        #region Public Methods
        public Chair() : base() {
            Station = StationClass.GetStation("Chair-Start");
        }
        // create Chair by parsing json string
        public Chair(string json) : base(json)
        {
            Station = StationClass.GetStation("Chair-Start");
        }
        // create a Chair from partNo, quantity, and a MAS connection
        public Chair(string partNo, int quantity) : base(partNo, quantity) {
            Station = StationClass.GetStation("Chair-Start");
        }
        // returns a JSON formatted string to be sent to a client
        public override string ExportTableRows(string clientType, int station)
        {
            string json = "";
            return json;
        }
        public override void ImportPart(IOrderManager orderManager, ref OdbcConnection MAS)
        {
            base.ImportPart(orderManager, ref MAS);
            // Chair info in the chair csv
            GetPackInfo(orderManager);
        }
        public override void AdvanceItem(ushort ID)
        {
            FindItem(ID).Station = GetNextStation(ID);
        }
        // labels
        public override string GetLabelFields(ushort itemID, LabelType type)
        {
            string json = "\"Barcode\":" + '"' + ID.ToString("D6") + '-' + itemID.ToString("D4") + '"'; // 11 digits [000000]-[0000]
            switch (type)
            {
                case LabelType.Tracking:
                    json += ",\"ID\":\"" + ID.ToString("D6") + '-' + itemID + "\"";
                    json += ",\"Desc1\":\"" + Part.BillNo + "\"";
                    json += ",\"Desc2\":\"" + Part.BillDesc + "\"";
                    break;
                case LabelType.Scrap:
                    json += ",\"ID\":\"" + ID.ToString("D6") + '-' + itemID + "\"";
                    json += ",\"Desc1\":\"" + Part.BillNo + "\"";
                    json += ",\"Desc2\":\"" + "!!!***SCRAP***!!!" + "\"";
                    break;
                case LabelType.Pack:
                    json += ",\"Order#\":\"" + (FindItem(itemID).Order != "" ? "Order: " + FindItem(itemID).Order : "To inventory") + "\"";
                    break;
            }
            return json;
        }
        // returns the next station for this chair
        public override int GetNextStation(UInt16 itemID)
        {
            int station = Items.Find(x => x.ID == itemID).Station;
            if (station == StationClass.GetStation("Start"))
            {
                return StationClass.GetStation("Start");
            }
            else if (station == StationClass.GetStation("Chairs"))
            {
                return StationClass.GetStation("Finished");

            }
            else if (station == StationClass.GetStation("Finished"))
            {
                return StationClass.GetStation("Finished");
            }
            return -1;
        }
        #endregion
            //--------------------------------------------------------
            #region Private Methods
        protected override string ExportProperties()
        {
            return ",\"type\":\"Chair\"";
        }
        private void GetPackInfo(IOrderManager orderManager)
        {
            //// open the table ref csv file
            //string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            //System.IO.StreamReader tableRef = new StreamReader(System.IO.Path.Combine(exeDir, "Table Reference.csv"));
            //tableRef.ReadLine(); // read past the header
            //string line = tableRef.ReadLine();
            //while (line != "" && line != null)
            //{
            //    string[] row = line.Split(',');
            //    if (Part.BillNo.Contains(row[0]))
            //    {
            //        //--------------------------------------------
            //        // PACK & BOX INFO
            //        //--------------------------------------------
                   
            //        break;
            //    }
            //    line = tableRef.ReadLine();
            //}
            //tableRef.Close();
        }
        #endregion
        //--------------------------------------------------------
        #region Properties

        // Labor
        private Item m_assm = null; // labor item
        private Item m_box = null; // labor item

        // Box
        private string m_boxItemCode = "";


        #endregion
        //--------------------------------------------------------
        #region Interface
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
        #endregion
        //--------------------------------------------------------
    }
}
