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
    internal class TableBox : Box
    {
        #region Public Methods
        public TableBox() : base()
        {

        }
        public TableBox(Table table) : base(table) {
            TableSize = table.Size;
            GetBoxSize("Table Reference.csv", table.ItemCode);
            foreach (Item componentItem in table.Part.ComponentBills[0].ComponentItems)
            {
                if (StationClass.GetStation("Box").LaborCodes.Exists(x => x == componentItem.ItemCode))
                {
                    BoxLabor = componentItem;
                    break;
                }
            }
        }
        // create Box by parsing json string
        public TableBox(string json) : base(json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                TableSize = obj["tableSize"];
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
            }
        }
        public override string ToString()
        {
            string inherited = base.ToString();
            Dictionary<string, string> obj = new StringStream(inherited).ParseJSON(false);
            obj.Add("tableSize", TableSize.Quotate());
            return obj.Stringify();
        }
        // returns a JSON formatted string to be sent to a client
        public override string ExportTableRows(string clientType, StationClass station)
        {
            try
            {
                Table parentTable = ((Table)ParentTravelers[0]);
                List<string> rows = new List<string>()
                {
                    new NameValueQty<string,string>("Table", parentTable.ItemCode,"").ToString(),
                    new NameValueQty<string,string>("Table Shape",parentTable.Shape,"").ToString(),
                    new NameValueQty<string, string>("Table Size", m_tableSize,"").ToString()
                };
                rows.AddRange(new StringStream(base.ExportTableRows(clientType, station)).ParseJSONarray(false));
                Dictionary<string, string> obj = new Dictionary<string, string>()
                {
                    {"members",rows.Stringify(false) }
                };
                return obj.Stringify();
            } catch (Exception ex)
            {
                Server.LogException(ex);
                return "";
            }
        }
        // labels
        public override string GetLabelFields(ushort itemID, LabelType type)
        {
            TravelerItem item = FindItem(itemID);
            string json = "\"Barcode\":" + '"' + ID.ToString("D6") + '-' + itemID.ToString("D4") + '"'; // 11 digits [000000]-[0000]
            switch (type)
            {
                case LabelType.Tracking:
                    json += ",\"ID\":\"" + "Box for " + ParentTravelers[0].ID.ToString("D6") + "\"";
                    json += ",\"Desc1\":\"" + BoxSize + "\"";
                    json += ",\"Desc2\":\"" + ((Table)ParentTravelers[0]).ItemCode + "\"";
                    json += ",\"Desc3\":\"" + PrintSequenceNo(item) + "\"";
                    break;
                case LabelType.Scrap:
                    json += ",\"ID\":\"" + "Box for " + ParentTravelers[0].ID.ToString("D6")+ "\"";
                    json += ",\"Desc1\":\"" + BoxSize + "\"";
                    json += ",\"Desc2\":\"" + "!! " + PrintSequenceNo(item) + " !!" + "\"";
                    json += ",\"Desc3\":\"" + "" + "\"";
                    break;
                case LabelType.Pack:
                    json += ",\"Order#\":\"" + (FindItem(itemID).Order != "" ? "Order: " + FindItem(itemID).Order : "To inventory") + "\"";
                    break;
            }
            return json;
        }
        #endregion
        //--------------------------------------------------------
        #region Private Methods
        #endregion
        //--------------------------------------------------------
        #region Properties
        // table size
        private string m_tableSize;
        #endregion
        //--------------------------------------------------------
        #region Interface
        public string TableSize
        {
            get
            {
                return m_tableSize;
            }

            set
            {
                m_tableSize = value;
            }
        }
        #endregion
        //--------------------------------------------------------
    }
}
