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
    public class TableBox : Box
    {
        #region Public Methods
        public TableBox() : base()
        {

        }
        public TableBox(Table table) : base(table) {
            TableSize = table.Size;
            
            ImportBoxSize("Table Reference.csv", table.ItemCode);
            foreach (Item componentItem in table.CommonBill.ComponentItems)
            {
                if (StationClass.GetStation("Box").LaborCodes.Exists(x => x == componentItem.ItemCode))
                {
                    BoxLabor = componentItem;
                    break;
                }
            }
        }
        // create Box by parsing json string
        public TableBox(string json,Version version) : base(json,version)
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
        public override string ExportTableRows(StationClass station)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(base.ExportTableRows(station)).ParseJSON(false);
                List<string> members = new StringStream(obj["members"]).ParseJSONarray(false);

                Table parentTable = (Table)ParentTravelers.FirstOrDefault();
                if (parentTable != null)
                {
                    members.Add(new NameValueQty<string, string>("Table", parentTable.ItemCode, "").ToString());
                    members.Add(new NameValueQty<string, string>("Table Shape", parentTable.Shape, "").ToString());
                    members.Add(new NameValueQty<string, string>("Table Size", m_tableSize, "").ToString());
                }
                obj["members"] = members.Stringify(false);
                return obj.Stringify();
            } catch (Exception ex)
            {
                Server.LogException(ex);
                return "";
            }
        }
        public override Dictionary<string, Node> ExportViewProperties()
        {
            Table parentTable = ParentTravelers.FirstOrDefault() as Table;
            Dictionary<string, Node> list = base.ExportViewProperties();
            list.Add("Fold Type", new TextNode(FoldType.ToString()));
            if (TwoPer) list.Add("Two per top/bottom", new TextNode("Yes"));
            list.Add("Pads", new TextNode(Pads.ToString()));
            if (parentTable != null)
            {
                list.Add("Table Shape", new TextNode(parentTable.Shape));
                list.Add("Table Size", new TextNode(parentTable.Size));
            }
            return list;
        }
        // labels
        public override string GetLabelFields(ushort itemID, LabelType type)
        {
            TravelerItem item = FindItem(itemID);
            Table parent = (Table)ParentTravelers.FirstOrDefault();
            string json = "\"Barcode\":" + '"' + ID.ToString("D6") + '-' + itemID.ToString("D4") + '"'; // 11 digits [000000]-[0000]
            switch (type)
            {
                case LabelType.Box:
                    json += ",\"ID\":\"" + "Box for " + (parent != null ? parent.ID.ToString("D6") : "Table") + "\"";
                    json += ",\"Desc1\":\"" + BoxSize + "\"";
                    json += ",\"Desc2\":\"" + (parent != null ? ((Table)ParentTravelers[0]).ItemCode + " (" + ((Table)ParentTravelers[0]).Size + ")" : "") + "\"";
                    json += ",\"Desc3\":\"" + "BOX" + "\"";
                    break;
                case LabelType.Scrap:
                    json += ",\"ID\":\"" + "Box for " + (parent != null ? parent.ID.ToString("D6") : "Table") + "\"";
                    json += ",\"Desc1\":\"" + BoxSize + "\"";
                    json += ",\"Desc2\":\"" + "!! " + PrintSequenceID(item) + " !!" + "\"";
                    ScrapEvent scrapEvent = FindItem(itemID).History.OfType<ScrapEvent>().ToList().Find(x => x.Process == ProcessType.Scrapped);
                    string reason = scrapEvent.Reason;
                    json += ",\"Reason\":" + reason.Quotate();
                    break;
                case LabelType.Pack:
                    json += ",\"Order#\":\"" + (FindItem(itemID).Order != null ? "Order: " + FindItem(itemID).Order.SalesOrderNo : "To inventory") + "\"";
                    break;
            }
            return json;
        }
        #endregion
        //--------------------------------------------------------
        #region Private Methods
        protected override void ImportBoxSize(string csvTable, string itemCode)
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
                    BoxSize = row[header.IndexOf("Super Pack")];
                    // Fold type
                    FoldType foldType;
                    if (!Enum.TryParse(row[header.IndexOf("Box Type")], out foldType)) foldType = FoldType.TD;
                    FoldType = foldType;
                    // 2 per top/btm
                    TwoPer = Convert.ToBoolean(row[header.IndexOf("2PerTopBottom")]);
                    // pads
                    Pads = Convert.ToInt32(row[header.IndexOf("Pads")]);
                    break;
                }
                line = tableRef.ReadLine();
            }
            tableRef.Close();
        }
        #endregion
        //--------------------------------------------------------
        #region Properties
        // table size
        private string m_tableSize;
        private bool m_twoPer; // 2 tops and 2 bottoms
        private int m_pads;
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

        private bool TwoPer
        {
            get
            {
                return m_twoPer;
            }

            set
            {
                m_twoPer = value;
            }
        }

        private int Pads
        {
            get
            {
                return m_pads;
            }

            set
            {
                m_pads = value;
            }
        }
        #endregion
        //--------------------------------------------------------
    }
}
