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
        public TravelerManager(OdbcConnection mas)
        {
            m_MAS = mas;
        }
        // Creates and combines travelers from the order list
        public void CompileTravelers()
        {
            int index = 0;
            foreach (Order order in m_orders)
            {
                Console.Write("\r{0}%   ", "Compiling Travelers..." + Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(m_orders.Count)) * 100));
                // Make a unique traveler for each order, while combining common parts from different models into single traveler
                bool foundBill = false;
                // search for existing traveler
                foreach (Traveler traveler in m_travelers)
                {
                    if (traveler.Part == null) traveler.ImportPart(MAS);
                    // only combine travelers if they have no events (meaning nothing has happened to them yet)
                    if (traveler.History.Count == 0 && traveler.Part.BillNo == order.ItemCode)
                    {
                        // update existing traveler
                        foundBill = true;
                        // add to the quantity of items
                        traveler.Quantity += order.QuantityOrdered;
                        // add to the order list
                        traveler.Orders.Add(order);
                    }
                }
                if (!foundBill)
                {
                    // create a new traveler from the new item
                    Traveler newTraveler = new Traveler(order.ItemCode, order.QuantityOrdered, MAS);
                    // add to the order list
                    newTraveler.Orders.Add(order);
                    // start the new traveler's journey
                    newTraveler.Start();
                    // add the new traveler to the list
                    m_travelers.Add(newTraveler);
                }
                index++;
            }
            Console.Write("\r{0}   ", "Compiling Travelers...Finished\n");
            ImportInformation();
        }
        // Resets the traveler and order lists
        public void Reset()
        {
            m_travelers.Clear();
            m_orders.Clear();
        }

        //-----------------------
        // Private members
        //-----------------------

        // return the traveler that containss the order number s
        protected Traveler FindTraveler(string s)
        {
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string line;
            System.IO.StreamReader file = new System.IO.StreamReader(System.IO.Path.Combine(exeDir, "printed.json"));
            int travelerID = 0;
            try
            {
                if (s.Length < 7)
                {
                    travelerID = Convert.ToInt32(s);
                }
            }
            catch (Exception ex)
            {

            }
            while ((line = file.ReadLine()) != null && line != "")
            {
                Traveler printedTraveler = new Traveler(line);
                // check to see if the number matches a traveler ID
                if (travelerID == printedTraveler.ID)
                {
                    return printedTraveler;
                }
                // check to see if these orders have been printed already
                foreach (Order printedOrder in printedTraveler.Orders)
                {
                    if (printedOrder.SalesOrderNo == s)
                    {
                        return printedTraveler;
                    }
                }
            }
            return null;
        }
        // Gathers part information about a traveler from MAS
        protected virtual void ImportInformation()
        {
            int index = 0;
            foreach (Traveler traveler in m_travelers)
            {
                if (traveler.Part == null) traveler.ImportPart(MAS);
                Console.Write("\r{0}%", "Importing Traveler Info..." + Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(m_travelers.Count)) * 100));
                traveler.CheckInventory(MAS);
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

        protected List<Order> m_orders = new List<Order>();
        protected List<Traveler> m_travelers = new List<Traveler>();
        protected OdbcConnection m_MAS = new OdbcConnection();

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
        internal OdbcConnection MAS
        {
            get
            {
                return m_MAS;
            }

            set
            {
                m_MAS = value;
            }
        }
    }
}
