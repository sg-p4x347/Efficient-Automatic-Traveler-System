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
        public static NodeList CreateMonitorHeader()
        {
            NodeList row = new NodeList(DOMtype: "tr");
            row.Add(new Node(KanbanManager.BorderStyle, DOMtype:"td"));
            row.Add(new TextNode("Current", KanbanManager.BorderStyle, DOMtype: "th"));
            row.Add(new TextNode("Min Qty", KanbanManager.BorderStyle, DOMtype: "th"));
            row.Add(new TextNode("Item Code", KanbanManager.BorderStyle, DOMtype: "th"));
            row.Add(new TextNode("Qty Queued", KanbanManager.BorderStyle, DOMtype: "th"));
            row.Add(new TextNode("Traveler Qty", KanbanManager.BorderStyle, DOMtype: "th"));
            return row;
        }
        public NodeList CreateMonitorRow()
        {
            NodeList row = new NodeList(DOMtype: "tr");
            double x = (double)Math.Max(0, m_stockQty - MinStockQty) / (double)MinStockQty;

            byte red = (byte)Math.Min(255, 2.0 * (1-x) * 255);
            //byte green = (byte)Math.Min(255, (((double)Math.Max(0, m_stockQty - MinStockQty) / (double)MinStockQty)) * (double)255);
            byte green = (byte)Math.Min(255,(255 * x * 2.0));
            
            Dictionary<string, string> colorBox = new Dictionary<string, string>()
            {
                {"backgroundColor", ("rgb(" + red + ',' + green + ",0)").Quotate()},
                {"width", "1em".Quotate() },
                {"height", "1em".Quotate() }
            };
            colorBox.Merge(KanbanManager.BorderStyle);
            row.Add(new Node(colorBox,DOMtype:"td"));
            row.Add(new TextNode(m_stockQty.ToString(), KanbanManager.BorderStyle, DOMtype:"td"));
            row.Add(new TextNode(m_minStockQty.ToString(), KanbanManager.BorderStyle, DOMtype: "td"));
            row.Add(new TextNode(ItemCode, KanbanManager.BorderStyle, DOMtype: "td"));
            row.Add(new TextNode(m_qtyOnTraveler.ToString(), KanbanManager.BorderStyle, DOMtype: "td"));
            row.Add(new TextNode(InjectionQty.ToString(), KanbanManager.BorderStyle, DOMtype: "td"));
            return row;
        }
        public void Update(int stockQty, int qtyOnTraveler)
        {
            m_stockQty = stockQty;
            m_qtyOnTraveler = qtyOnTraveler;
            if (m_stockQty + m_qtyOnTraveler < m_minStockQty)
            {
                // ADD A NEW TRAVELER (TO START)
                Server.TravelerManager.AddTraveler(m_itemCode, m_injectionQty);
            }
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
