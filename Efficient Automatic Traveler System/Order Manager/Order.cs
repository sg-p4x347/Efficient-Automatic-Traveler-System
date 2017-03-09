using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    class OrderItem
    {
        public OrderItem() {
            ItemCode = "";
            QtyOrdered = 0;
            QtyOnHand = 0;
            ChildTraveler = -1;
        }
        public OrderItem(string json)
        {
            try
            {
                StringStream ss = new StringStream(json);
                Dictionary<string, string> obj = ss.ParseJSON();
                ItemCode = obj["itemCode"];
                QtyOrdered = Convert.ToInt32(obj["qtyOrdered"]);
                QtyOnHand = Convert.ToInt32(obj["qtyOnHand"]);
                ChildTraveler = Convert.ToInt32(obj["childTraveler"]);
            } catch (Exception ex)
            {
                Server.WriteLine("Error while reading OrderItem from file: " + ex.Message);
            }
        }
        public OrderItem(string i,int ordered,int onHand,int c)
        {
            ItemCode = i;
            QtyOrdered = ordered;
            QtyOnHand = onHand;
            ChildTraveler = c;
        }
        public string Export()
        {
            var json = "{";
            json += "\"itemCode\":" + '"' + ItemCode + '"' + ',';
            json += "\"qtyOrdered\":" + QtyOrdered + ',';
            json += "\"qtyOnHand\":" + QtyOnHand + ',';
            json += "\"childTraveler\":" + ChildTraveler;
            json += "}";
            return json;
        }
        public string ItemCode;
        public int QtyOrdered;
        public int QtyOnHand;
        public int ChildTraveler;
    }
    class Order
    {
        //-----------------------
        // Public members
        //-----------------------
        public Order() : base()
        {
            m_orderDate = DateTime.Today;
            m_salesOrderNo = "";
            m_customerNo = "";
            m_items = new List<OrderItem>();
            m_shipVia = "";
        }
        // Import from json string
        public Order(string json)
        {
            try
            {
                StringStream ss = new StringStream(json);
                Dictionary<string, string> obj = ss.ParseJSON();
                m_salesOrderNo = obj["salesOrderNo"];
                m_items = new List<OrderItem>();
                ss = new StringStream(obj["items"]);
                foreach (string item in ss.ParseJSONarray())
                {
                    m_items.Add(new OrderItem(item));
                }
            } catch (Exception ex)
            {
                Server.WriteLine("Error while reading order from file: " + ex.Message);
            }
            
        }
        public List<OrderItem> FindItems(int travelerID)
        {
            return m_items.Where(x => x.ChildTraveler == travelerID).ToList();
        }
        public string Export()
        {
            string json = "{";
            json += "\"salesOrderNo\":" + '"' + m_salesOrderNo + '"' + ',';
            json += "\"items\":";
            json += "[";
            foreach (OrderItem item in m_items)
            {
                if (item != m_items[0]) json += ',';
                json += item.Export();
            }
            json += "]";
            json += "}\n";
            return json;
        }
        //-----------------------
        // Private members
        //-----------------------
        //-----------------------
        // Properties
        //-----------------------
        private DateTime m_orderDate;
        private DateTime m_shipDate;
        private string m_salesOrderNo;
        private string m_customerNo;
        private List<OrderItem> m_items;
        private string m_shipVia;
        
        public DateTime OrderDate
        {
            get
            {
                return m_orderDate;
            }

            set
            {
                m_orderDate = value;
            }
        }

        public string SalesOrderNo
        {
            get
            {
                return m_salesOrderNo;
            }

            set
            {
                m_salesOrderNo = value;
            }
        }

        public string CustomerNo
        {
            get
            {
                return m_customerNo;
            }

            set
            {
                m_customerNo = value;
            }
        }

        public List<OrderItem> Items
        {
            get
            {
                return m_items;
            }

            set
            {
                m_items = value;
            }
        }
        public string ShipVia
        {
            get
            {
                return m_shipVia;
            }

            set
            {
                m_shipVia = value;
            }
        }

        public DateTime ShipDate
        {
            get
            {
                return m_shipDate;
            }

            set
            {
                m_shipDate = value;
            }
        }
    }
}
