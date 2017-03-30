using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    enum OrderState
    {
        Open,
        Closed
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
            m_state = OrderState.Open;
        }
        // Import from json string
        public Order(string json)
        {
            try
            {
                StringStream ss = new StringStream(json);
                Dictionary<string, string> obj = ss.ParseJSON();
                m_salesOrderNo = obj["salesOrderNo"];
                m_state = (OrderState)Enum.Parse(typeof(OrderState), obj["state"]);
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
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"salesOrderNo",m_salesOrderNo.Quotate() },
                {"state",m_state.ToString().Quotate() },
                {"items",m_items.Stringify<OrderItem>() }
            };
            return obj.Stringify();
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
        private OrderState m_state;
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

        internal OrderState State
        {
            get
            {
                return m_state;
            }

            set
            {
                m_state = value;
            }
        }
    }
}
