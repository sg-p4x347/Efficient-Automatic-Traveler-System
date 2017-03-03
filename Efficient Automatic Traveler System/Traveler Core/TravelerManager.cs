using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Odbc;

namespace Efficient_Automatic_Traveler_System
{
    class TravelerManager
    {
        //-----------------------
        // Public members
        //-----------------------

        public TravelerManager() { }
        public TravelerManager(ref OdbcConnection mas, ref List<Order> orders, ref List<Traveler> travelers)
        {
            m_MAS = mas;
            m_orders = orders;
            m_travelers = travelers;
        }
        // Creates and combines travelers from the order list
        public virtual void CompileTravelers(ref List<Order> orders)
        {
            
        }
        // Relational
        public int FindOrderIndex(ref List<Order> orders, string orderNo)
        {
            for (int index = 0; index < m_orders.Count; index++)
            {
                if (orders[index].SalesOrderNo == orderNo) return index;
            }
            return -1;
        }
        public static int FindOrderItemIndex(ref Order order, int travelerID)
        {
            for (int index = 0; index < order.Items.Count; index++)
            {
                if (order.Items[index].ChildTraveler == travelerID) return index;
            }
            return -1;
        }
        public void UpdateQuantity(Traveler traveler)
        {
            // 1.) compensate highest level traveler with inventory
            // if it has parent orders and hasn't started, the quantity can change
            if (traveler.LastStation == Traveler.GetStation("Start") && traveler.ParentOrders.Count > 0)
            {
                traveler.Quantity = QuantityNeeded(traveler);
            }
            // 2.) adjust children quantities
            if (traveler.Children.Count > 0)
            {
                int qtyNeeded = Math.Max(0,QuantityNeeded(traveler) - traveler.Quantity);
                List<Traveler> started = new List<Traveler>();
                List<Traveler> notStarted = new List<Traveler>();
                foreach (int childID in traveler.Children)
                {
                    Traveler child = FindTraveler(childID);
                    if (child != null)
                    {
                        // can only change quantity if this child hasn't started
                        if (child.LastStation == Traveler.GetStation("Start"))
                        {
                            notStarted.Add(child);
                        } else
                        {
                            started.Add(child);
                            qtyNeeded -= child.Quantity;
                        }
                    }
                }
                foreach (Traveler child in notStarted)
                {
                    if (qtyNeeded == 0)
                    {
                        m_travelers.Remove(child); // don't need this anymore
                        traveler.Children.RemoveAll(x => x == child.ID);
                    } else
                    {
                        child.Quantity = qtyNeeded;
                        qtyNeeded = 0;
                    }
                }
            }
        }
        //-----------------------
        // Private members
        //-----------------------
        private Traveler FindTraveler(int ID)
        {
            return m_travelers.Find(x => x.ID == ID);
        }

        // sets the quantity as a minimum of what is on hand, maxing at what was ordered
        //protected void UpdateQuantity(Traveler traveler)
        //{
        //    traveler.Quantity = 0;
        //    // compensate for inventory (order item has already calculated how much is on hand for itself)
        //    foreach (string orderNo in traveler.ParentOrders)
        //    {
        //        Order order = m_orders[FindOrderIndex(ref m_orders, orderNo)];
        //        foreach (OrderItem item in order.Items)
        //        {
        //            if (item.ChildTraveler == traveler.ID)
        //            {
        //                traveler.Quantity += Math.Max(0, item.QtyOrdered-item.QtyOnHand);
        //            }
        //        }
        //    }
        //}
        // Gets the total quantity ordered, compensated by what is in stock
        protected int QuantityNeeded(Traveler traveler)
        {
            int qtyNeeded = 0;
            foreach (string orderNo in traveler.ParentOrders)
            {
                Order order = m_orders[FindOrderIndex(ref m_orders, orderNo)];
                foreach (OrderItem item in order.Items)
                {
                    if (item.ChildTraveler == traveler.ID)
                    {
                        qtyNeeded += Math.Max(0, item.QtyOrdered - item.QtyOnHand);
                    }
                }
            }
            return qtyNeeded;
        }
        // Gathers part information about a traveler from MAS
        public virtual void ImportInformation()
        {
            int index = 0;
            foreach (Traveler traveler in m_travelers)
            {
                if (traveler.Part == null) traveler.ImportPart(ref m_MAS);
                Console.Write("\r{0}%", "Importing Traveler Info..." + Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(m_travelers.Count)) * 100));
                traveler.CheckInventory(ref m_MAS);
                // update and total the final parts
                traveler.Part.TotalQuantity = traveler.Quantity;
                traveler.FindComponents(traveler.Part);
                index++;
            }
            Console.Write("\r{0}", "Importing Traveler Info...Finished\n");
        }
        
        //-----------------------
        // Properties
        //-----------------------

        protected List<Order> m_orders;
        protected List<Traveler> m_travelers;
        protected OdbcConnection m_MAS;

        internal List<Order> Orders
        {
            get
            {
                return m_orders;
            }

            set
            {
                m_orders = value;
            }
        }
        internal List<Traveler> Travelers
        {
            get
            {
                return m_travelers;
            }
        }
    }
}
