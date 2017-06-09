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
    internal class Chair : Part
    {
        #region Public Methods
        public Chair() : base() {
            Bill = null;
        }
        public Chair(Form form) : base(form)
        {
            Bill = new Bill(form.ValueOf("itemCode"), 1, Convert.ToInt32(form.ValueOf("quantity")));
        }
        public Chair(string json) : base(json) {
        }
        public Chair(string itemCode, int quantity) : base(itemCode,quantity) {
        }
        // returns a JSON formatted string to be sent to a client
        public override string ExportTableRows(string clientType, StationClass station)
        {
            string json = "";
            return json;
        }
        public async override Task ImportInfo(ITravelerManager travelerManager, IOrderManager orderManager, OdbcConnection MAS)
        {
            await base.ImportInfo(travelerManager, orderManager,MAS);
        }
        public override
        // labels
        public override string GetLabelFields(ushort itemID, LabelType type)
        {
            TravelerItem item = FindItem(itemID);
            string json = "\"Barcode\":" + '"' + ID.ToString("D6") + '-' + itemID.ToString("D4") + '"'; // 11 digits [000000]-[0000]
            switch (type)
            {
                case LabelType.Tracking:
                    json += ",\"ID\":\"" + PrintSequenceID(item) + "\"";
                    json += ",\"Desc1\":\"" + Part.BillNo + "\"";
                    json += ",\"Desc2\":\"" + Part.BillDesc + "\"";
                    break;
                case LabelType.Scrap:
                    json += ",\"ID\":\"" + PrintSequenceID(item) + "\"";
                    json += ",\"Desc1\":\"" + Part.BillNo + "\"";
                    json += ",\"Desc2\":\"" + "!!" + PrintSequenceID(item) + "!!" + "\"";
                    break;
                case LabelType.Pack:
                    json += ",\"Order#\":\"" + (FindItem(itemID).Order != "" ? "Order: " + FindItem(itemID).Order : "To inventory") + "\"";
                    break;
            }
            return json;
        }
        // returns the next station for this chair
        public override StationClass GetNextStation(UInt16 itemID)
        {
            StationClass station = Items.Find(x => x.ID == itemID).Station;
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
            else
            {
                return station;
            }
        }
        #endregion
        //--------------------------------------------------------
        #region Private Methods
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

        public override double GetTotalLabor(StationClass station)
        {
            throw new NotImplementedException();
        }

        public override bool CombinesWith(object[] args)
        {
            throw new NotImplementedException();
        }

        public override void AdvanceItem(ushort ID, ITravelerManager travelerManager = null)
        {
            throw new NotImplementedException();
        }

        public override double GetCurrentLabor(StationClass station = null)
        {
            throw new NotImplementedException();
        }

        #endregion
        //--------------------------------------------------------
        #region Properties

        // Labor
        private Item m_assm = null; // labor item
        private Item m_box = null; // labor item

        #endregion
        //--------------------------------------------------------
        #region Interface
        #endregion
        //--------------------------------------------------------
    }
}
