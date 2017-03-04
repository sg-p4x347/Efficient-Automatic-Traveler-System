using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Odbc;

namespace Efficient_Automatic_Traveler_System
{
    abstract class TravelerManager
    {
        //-----------------------
        // Public members
        //-----------------------

        public TravelerManager() { }
        public TravelerManager(ref OdbcConnection mas, ITravelerCore travelerCore)
        {
            m_MAS = mas;
            m_travelerCore = travelerCore;
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
                    Traveler child = m_travelerCore.FindTraveler(childID);
                    if (child != null)
                    {
                        // update children of child
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
                        m_travelerCore.GetTravelers.Remove(child); // don't need this anymore
                        traveler.Children.RemoveAll(x => x == child.ID);
                    } else
                    {
                        child.Quantity = qtyNeeded;
                        qtyNeeded = 0;
                    }
                }
            }
        }
        // Creates and combines travelers from the order list
        //public virtual void CompileTravelers(ref List<Order> newOrders)
        //{
        //    int index = 0;
        //    for (int orderIndex = 0; orderIndex < newOrders.Count; orderIndex++)
        //    {
        //        Order order = newOrders[orderIndex];
        //        for (int itemIndex = 0; itemIndex < order.Items.Count; itemIndex++)
        //        {
        //            OrderItem item = order.Items[itemIndex];
        //            // only make a traveler if this one has no child traveler already (-1 signifies no child traveler)
        //            if (item.ChildTraveler < 0 && InstanceOf(item.ItemCode))
        //            {
        //                Console.Write("\r{0}%   ", "Compiling Travelers..." + Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(newOrders.Count)) * 100));

        //                // search for existing traveler
        //                // can only combine if same itemCode, hasn't started, and has no parents
        //                Traveler traveler = m_travelers.Find(x => x.ItemCode == item.ItemCode && x.LastStation == Traveler.GetStation("Start") && x.Parents.Count == 0);
        //                if (traveler != null)
        //                {
        //                    // add to existing traveler
        //                    traveler.Quantity += item.QtyOrdered;

        //                    // RELATIONAL =============================================================
        //                    item.ChildTraveler = traveler.ID;
        //                    traveler.ParentOrders.Add(order.SalesOrderNo);
        //                    //=========================================================================
        //                }
        //                else
        //                {
        //                    Traveler toot = (Traveler.IsTable(item.ItemCode) ? (Traveler)new Table() : (Traveler)new Table());
        //                    // create a new traveler from the new item
        //                    Traveler newTraveler = NewDerivedInstance(item.ItemCode, item.QtyOrdered, ref m_MAS);

        //                    // RELATIONAL =============================================================
        //                    item.ChildTraveler = newTraveler.ID;
        //                    newTraveler.ParentOrders.Add(order.SalesOrderNo);
        //                    //=========================================================================

        //                    // start the new traveler's journey
        //                    newTraveler.Start();
        //                    // add the new traveler to the list
        //                    m_travelers.Add(newTraveler);
        //                }
        //            }
        //        }
        //        index++;
        //    }
        //    Console.Write("\r{0}   ", "Compiling Tables...Finished\n");
        //}
        //-----------------------
        // Private members
        //-----------------------
        //protected abstract bool InstanceOf(string itemCode);
        //protected abstract Traveler NewDerivedInstance(string itemCode, int qty, ref OdbcConnection mas);


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
                Order order = m_travelerCore.FindOrder(orderNo);
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
        public abstract void FinalizeTravelers();
        // Gathers part information about a traveler from MAS
        //public void FinalizeTravelers()
        //{
        //    int index = 0;
        //    foreach (Traveler traveler in m_travelers)
        //    {
        //        // compensate for items covered by inventory (already calculated for the order item)
        //        UpdateQuantity(traveler);
        //    }
        //    ImportInformation();
        //        if (InstanceOf(traveler.ItemCode))
        //        {
        //            ImportInformation(traveler); // implemented by derived managers

        //            // update and total the final parts
        //            traveler.Part.TotalQuantity = traveler.Quantity;
        //            traveler.FindComponents(traveler.Part);
        //            index++;
        //        }

        //    Console.Write("\r{0}", "Importing Traveler Info...Finished\n");
        //}
        //protected abstract void ImportInformation();

        //-----------------------
        // Properties
        //-----------------------

        protected ITravelerCore m_travelerCore;
        protected OdbcConnection m_MAS;
    }
}
