﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    public enum OrderStatus
    {
        Open,
        Closed,
        Hold,
        Removed
    }
    public class Order
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
            m_status = OrderStatus.Open;
            m_invoicing = false;
        }
        // Import from json string
        public Order(string json)
        {
            try
            {
                StringStream ss = new StringStream(json);
                Dictionary<string, string> obj = ss.ParseJSON();
                m_salesOrderNo = obj["salesOrderNo"];
                m_status = (OrderStatus)Enum.Parse(typeof(OrderStatus), obj["state"]);
                m_items = new List<OrderItem>();
                ss = new StringStream(obj["items"]);
                foreach (string item in ss.ParseJSONarray())
                {
                    m_items.Add(new OrderItem(item,this));
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
                {"state",m_status.ToString().Quotate() },
                {"items",m_items.Stringify<OrderItem>() }
            };
            return obj.Stringify();
        }
        public int CompareTo(Order b)
        {
            // sort by ship date first, then by customer
            int placement = ShipDate.CompareTo(b.ShipDate);
            if (placement == 0)
            {
                if (((JsonArray)JSON.Parse(ConfigManager.Get("priorityCustomers"))).ToList().Contains(CustomerNo))
                {
                    return -1;
                } else
                {
                    return 1;
                }
            } else
            {
                return placement;
            }
            
        }
        public void SetStatus(char ch)
        {
            switch (ch)
            {
                case 'O': Status = OrderStatus.Open; break;
                case 'C': Status = OrderStatus.Closed; break;
                case 'H': Status = OrderStatus.Hold; break;
            }
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
        private bool m_invoicing;
        private OrderStatus m_status;
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

        public OrderStatus Status
        {
            get
            {
                return m_status;
            }

            set
            {
                m_status = value;
            }
        }

        public bool Invoicing
        {
            get
            {
                return m_invoicing;
            }

            set
            {
                m_invoicing = value;
            }
        }
    }
}
