using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Odbc;
using System.Diagnostics;
using System.IO;
using Excel = Microsoft.Office.Interop.Excel;
using Marshal = System.Runtime.InteropServices.Marshal;

namespace Efficient_Automatic_Traveler_System
{
    class ChairManager
    {
        public ChairManager() { }
        public ChairManager(OdbcConnection mas) {
            m_MAS = mas;
        }
        //=======================
        // Travelers
        //=======================
        public void CompileTravelers()
        {
            Console.WriteLine("");
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            // clear any previous travelers
            m_travelers.Clear();
            
            //==========================================
            // compile the travelers
            //==========================================
            int index = 0;
            foreach (Order order in m_orders)
            {
                Console.Write("\r{0}%   ", "Compiling Chairs..." + Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(m_orders.Count)) * 100));
                // Make a unique traveler for each order, while combining common parts from different models into single traveler
                bool foundBill = false;
                // search for existing traveler
                foreach (Chair traveler in m_travelers)
                {
                    if (traveler.Part == null) traveler.ImportPart(MAS);
                    if (traveler.Part.BillNo == order.ItemCode)
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
                    Chair newTraveler = new Chair(order.ItemCode, order.QuantityOrdered, MAS);
                    // add to the order list
                    newTraveler.Orders.Add(order);
                    // add the new traveler to the list
                    m_travelers.Add(newTraveler);
                }
                index++;
            }
            Console.Write("\r{0}   ", "Compiling Chairs...Finished");
            ImportInformation();
        }
        private void ImportInformation()
        {
            Console.WriteLine("");
            int index = 0;
            foreach (Chair traveler in m_travelers)
            {
                if (traveler.Part == null) traveler.ImportPart(MAS);
                Console.Write("\r{0}%   ", "Importing Chair Info..." + Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(m_travelers.Count)) * 100));
                traveler.CheckInventory(MAS);
                // update and total the final parts
                traveler.Part.TotalQuantity = traveler.Quantity;
                traveler.FindComponents(traveler.Part);
                // chair specific
                GetBoxInfo(traveler);
            }
            Console.Write("\r{0}   ", "Importing Chair Info...Finished");
        }
        private void GetBoxInfo(Chair traveler)
        {
            if (traveler.PartNo[traveler.PartNo.Length-1] == '4')
            {
                traveler.PartsPerBox = 4;
            } else
            {
                traveler.PartsPerBox = 6;
            }
            traveler.RegPackQty = traveler.Quantity / traveler.PartsPerBox;
        }
        private bool IsChair(string s)
        {
            if (s.Length == 14 && s.Substring(0, 2) == "38")
            {
                string[] parts = s.Split('-');
                return (parts[0].Length == 5 && parts[1].Length == 4 && parts[2].Length == 3);
            }
            else if (s.Length == 15 && s.Substring(0, 4) == "MG11")
            {
                string[] parts = s.Split('-');
                return (parts[0].Length == 6 && parts[1].Length == 4 && parts[2].Length == 3);
            }
            else
            {
                return false;
            }

        }
        //=======================
        // Properties
        //=======================
        private List<Order> m_orders = new List<Order>();
        private List<Chair> m_travelers = new List<Chair>();
        private OdbcConnection m_MAS = null;

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

        internal List<Chair> Travelers
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
