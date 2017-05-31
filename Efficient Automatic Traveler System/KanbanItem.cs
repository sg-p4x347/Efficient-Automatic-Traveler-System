using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    class KanbanItem
    {
        #region Public Methods
        public KanbanItem(Form form)
        {
            Update(form);
        }
        public KanbanItem(string json)
        {
            Dictionary<string, string> obj = new StringStream(json).ParseJSON();
            m_itemCode = obj["itemCode"];
            m_minStockQty = Convert.ToInt32(obj["minStockQty"]);
            m_injectionQty = Convert.ToInt32(obj["injectionQty"]);
        }
        public KanbanItem(string itemCode, int minStockQty, int injectionQty)
        {
            m_itemCode = itemCode;
            m_minStockQty = minStockQty;
            m_injectionQty = injectionQty;
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"itemCode",m_itemCode.Quotate() },
                {"minStockQty", m_minStockQty.ToString() },
                {"injectionQty", m_injectionQty.ToString() }
            };
            return obj.Stringify();
        }
        public NodeList CreateMonitorRow()
        {
            NodeList row = new NodeList(DOMtype: "tr");
            byte red = (byte)Math.Min(255,255 - ((m_stockQty - MinStockQty) / MinStockQty) * 255);
            byte green = (byte)Math.Min(255, ((m_stockQty - MinStockQty) / MinStockQty) * 255);
            Dictionary<string, string> colorBox = new Dictionary<string, string>()
            {
                {"backgroundColor", ("rgb(" + red + ',' + green + ",0)").Quotate()},
                {"width", "1em".Quotate() },
                {"height", "1em".Quotate() }
            };
            row.Add(new Node(colorBox,"td"));
            row.Add(new TextNode(m_stockQty.ToString(), DOMtype:"td"));
            row.Add(new TextNode(m_minStockQty.ToString(), DOMtype: "td"));
            row.Add(new TextNode(ItemCode, DOMtype: "td"));
            row.Add(new TextNode(InjectionQty.ToString(), DOMtype: "td"));
            return row;
        }
        public void Update(int stockQty, int qtyOnTraveler)
        {
            m_stockQty = stockQty;
            m_qtyOnTraveler = qtyOnTraveler;
        }

        public static Form CreateForm()
        {
            Form form = new Form();
            form.Title = "Kanban Item";
            form.Textbox("itemCode", "Item Code");
            form.Integer("minStockQty", "Minimum balance",10);
            form.Integer("injectionQty", "Traveler quantity", 10);
            return form;
        }

        public Form CreateFilledForm()
        {
            throw new NotImplementedException();
        }

        public void Update(Form form)
        {
            m_itemCode = form.ValueOf("itemCode");
            m_minStockQty = Convert.ToInt32(form.ValueOf("minStockQty"));
            m_injectionQty = Convert.ToInt32(form.ValueOf("injectionQty"));
        }
        #endregion
        #region Properties
        private string m_itemCode;
        private int m_stockQty;
        private int m_qtyOnTraveler;
        private int m_minStockQty;
        private int m_injectionQty; // cuz it sounds cool


        #endregion
        #region Interface
        public string ItemCode
        {
            get
            {
                return m_itemCode;
            }

            set
            {
                m_itemCode = value;
            }
        }

        public int MinStockQty
        {
            get
            {
                return m_minStockQty;
            }

            set
            {
                m_minStockQty = value;
            }
        }

        public int InjectionQty
        {
            get
            {
                return m_injectionQty;
            }

            set
            {
                m_injectionQty = value;
            }
        }
        #endregion
    }
}
