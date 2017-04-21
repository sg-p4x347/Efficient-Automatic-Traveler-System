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
    internal class Box : Traveler
    {
        #region Public Methods
        public Box() : base()
        {
            Station = StationClass.GetStation("Box");
            m_boxSize = "";
        }
        // create Box by parsing json string
        public Box(string json) : base(json)
        {
            Station = StationClass.GetStation("Box");
        }
        // create a Box for a table
        public Box(Table table) : base()
        {

            Station = StationClass.GetStation("Box");
            GetBoxSize("Table Reference.csv",table.ItemCode);
            foreach (Item componentItem in table.Part.ComponentBills[0].ComponentItems)
            {
                if (StationClass.GetStation("Box").LaborCodes.Exists(x => x == componentItem.ItemCode))
                {
                    m_box = componentItem;
                    break;
                }
            }
            m_quantity = table.Quantity;
            ParentTravelers.Add(table);
        }
        // returns a JSON formatted string to be sent to a client
        public override string ExportTableRows(string clientType, StationClass station)
        {
            List<string> rows = new List<string>()
            {
                new NameValueQty<string, string>("Box Size", m_boxSize,"").ToString()
            };
            return rows.Stringify(false).TrimStart('[').TrimEnd(']');
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
                    json += ",\"Desc1\":\"" + "" + "\"";
                    json += ",\"Desc2\":\"" + "" + "\"";
                    break;
                case LabelType.Scrap:
                    json += ",\"ID\":\"" + ID.ToString("D6") + '-' + itemID + "\"";
                    json += ",\"Desc1\":\"" + "" + "\"";
                    json += ",\"Desc2\":\"" + "!!!***SCRAP***!!!" + "\"";
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
        protected override string ExportProperties()
        {
            return ",\"type\":\"Chair\"";
        }
        private void GetBoxSize(string csvTable, string itemCode)
        {
            // open the table ref csv file
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            System.IO.StreamReader tableRef = new StreamReader(System.IO.Path.Combine(exeDir, csvTable));
            // read past the header
            List<string> header = tableRef.ReadLine().Split(',').ToList();
            string line = tableRef.ReadLine();
            while (line != "" && line != null)
            {
                string[] row = line.Split(',');
                if (itemCode.Contains(row[header.IndexOf("Table")]))
                {
                    //--------------------------------------------
                    // PACK & BOX INFO
                    //--------------------------------------------
                    m_boxSize = row[header.IndexOf("Super Pack")];
                    break;
                }
                line = tableRef.ReadLine();
            }
            tableRef.Close();
        }

        public override double GetCurrentLabor()
        {
            return (m_box != null ? m_box.QuantityPerBill : 0.0);
        }

        public override double GetTotalLabor(StationClass station)
        {
            throw new NotImplementedException();
        }
        #endregion
        //--------------------------------------------------------
        #region Properties

        // Box
        private string m_boxSize;
        // labor
        private Item m_box;
        #endregion
        //--------------------------------------------------------
        #region Interface
        public string BoxSize
        {
            get
            {
                return m_boxSize;
            }

            set
            {
                m_boxSize = value;
            }
        }
        #endregion
        //--------------------------------------------------------
    }
}
