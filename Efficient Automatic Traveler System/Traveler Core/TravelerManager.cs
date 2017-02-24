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
        public int FindOrderItemIndex(ref Order order, int travelerID)
        {
            for (int index = 0; index < order.Items.Count; index++)
            {
                if (order.Items[index].ChildTraveler == travelerID) return index;
            }
            return -1;
        }
        //-----------------------
        // Private members
        //-----------------------

        // return the traveler that containss the order number s
        //protected Traveler FindTraveler(string s)
        //{
        //    string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        //    string line;
        //    System.IO.StreamReader file = new System.IO.StreamReader(System.IO.Path.Combine(exeDir, "printed.json"));
        //    int travelerID = 0;
        //    try
        //    {
        //        if (s.Length < 7)
        //        {
        //            travelerID = Convert.ToInt32(s);
        //        }
        //    }
        //    catch (Exception ex)
        //    {

        //    }
        //    while ((line = file.ReadLine()) != null && line != "")
        //    {
        //        Traveler printedTraveler = new Traveler(line);
        //        // check to see if the number matches a traveler ID
        //        if (travelerID == printedTraveler.ID)
        //        {
        //            return printedTraveler;
        //        }
        //        // check to see if these orders have been printed already
        //        foreach (Order printedOrder in printedTraveler.Orders)
        //        {
        //            if (printedOrder.SalesOrderNo == s)
        //            {
        //                return printedTraveler;
        //            }
        //        }
        //    }
        //    return null;
        //}
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
