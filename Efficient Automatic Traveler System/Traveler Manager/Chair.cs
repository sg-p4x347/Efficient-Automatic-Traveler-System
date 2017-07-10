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
    public class Chair : Part
    {
        #region Public Methods
        public Chair() : base() {
            Bill = null;
        }
        public Chair(Form form) : base(form)
        {
            Bill = new Bill(form.ValueOf("itemCode"), 1, Convert.ToInt32(form.ValueOf("quantity")));
        }
        public Chair(string json,Version version) : base(json,version) {
        }
        public Chair(string itemCode, int quantity) : base(itemCode,quantity) {
        }
        // returns a JSON formatted string to be sent to a client
        public override string ExportTableRows(StationClass station)
        {
            return base.ExportTableRows(station);
        }
        public override void ImportInfo(ITravelerManager travelerManager, IOrderManager orderManager, OdbcConnection MAS)
        {
            base.ImportInfo(travelerManager, orderManager,MAS);
        }
        // labels
        public override string GetLabelFields(ushort itemID, LabelType type)
        {
            TravelerItem item = FindItem(itemID);
            string json = "\"Barcode\":" + '"' + ID.ToString("D6") + '-' + itemID.ToString("D4") + '"'; // 11 digits [000000]-[0000]
            switch (type)
            {
                case LabelType.Chair:
                    json += ",\"Barcode\":" + '"' + ID.ToString("D6") + '-' + itemID.ToString("D4") + '"'; // 11 digits [000000]-[0000]
                    json += GetLabelFields(new List<string>()
                    {
                        "Marco Item #",
                        "DescriptionShort",
                        "Color1",
                        "Color2"
                    });
                    break;
                case LabelType.Scrap:
                    json += ",\"ID\":\"" + PrintSequenceID(item) + "\"";
                    json += ",\"Desc1\":\"" + Bill.BillNo + "\"";
                    json += ",\"Desc2\":\"" + "!!" + PrintSequenceID(item) + "!!" + "\"";
                    break;
                case LabelType.Pack:
                    json += ",\"Order#\":\"" + (FindItem(itemID).Order != null ? "Order: " + FindItem(itemID).Order.SalesOrderNo : "To inventory") + "\"";
                    json += GetLabelFields(new List<string>()
                    {
                        "Marco Item #",
                        "DescriptionShort",
                        "Color1",
                        "Color2",
                        "PcsPerCarton"
                    });
                    break;
                case LabelType.MixedCarton:
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
            return 0.0;
        }

        public override double GetCurrentLabor(StationClass station = null)
        {
            return Bill.LaborAt(station);
        }

        #endregion
        #region Private Methods
        
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
