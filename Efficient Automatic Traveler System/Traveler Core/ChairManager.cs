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
    class ChairManager : TravelerManager
    {
        //-----------------------
        // Public members
        //-----------------------
        public ChairManager() : base(){ }
        public ChairManager(ref OdbcConnection mas, ref List<Order> orders, ref List<Traveler> travelers) : base(ref mas, ref orders, ref travelers) {

        }
        public override void CompileTravelers(ref List<Order> newOrders)
        {
            int index = 0;
            foreach (Order order in newOrders)
            {
                foreach (OrderItem item in order.Items)
                {
                    if (Traveler.IsChair(item.ItemCode))
                    {
                        Console.Write("\r{0}%   ", "Compiling Travelers..." + Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(newOrders.Count)) * 100));
                        // Make a unique traveler for each order, while combining common parts from different models into single traveler
                        bool foundBill = false;
                        // search for existing traveler
                        foreach (Traveler traveler in m_travelers)
                        {
                            if (traveler.Part == null) traveler.ImportPart(ref m_MAS);
                            // only combine travelers if they have no events (meaning nothing has happened to them yet)
                            if (traveler.History.Count == 0 && traveler.Part.BillNo == item.ItemCode)
                            {
                                // update existing traveler
                                foundBill = true;
                                // add to the quantity of items
                                traveler.Quantity += item.QtyOrdered;
                                // add to the order list
                                traveler.ParentOrders.Add(order.SalesOrderNo);
                            }
                        }
                        if (!foundBill)
                        {
                            // create a new traveler from the new item
                            Chair newTraveler = new Chair(item.ItemCode, item.QtyOrdered, ref m_MAS);
                            item.ChildTraveler = newTraveler.ID;
                            // add to the order list
                            newTraveler.ParentOrders.Add(order.SalesOrderNo);
                            // start the new traveler's journey
                            newTraveler.Start();
                            // add the new traveler to the list
                            m_travelers.Add(newTraveler);
                        }

                    }
                }
                index++;
            }
            Console.Write("\r{0}   ", "Compiling Travelers...Finished\n");
        }
        public override void ImportInformation()
        {
            int index = 0;
            foreach (Chair chair in m_travelers.OfType<Chair>())
            {
                if (chair.Part == null) chair.ImportPart(ref m_MAS);
                Server.Write("\r{0}%", "Importing Chair Info..." + Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(m_travelers.Count)) * 100));
                chair.CheckInventory(ref m_MAS);
                // update and total the final parts
                chair.Part.TotalQuantity = chair.Quantity;
                chair.FindComponents(chair.Part);
                // chair specific
                GetBoxInfo(chair);
            }
            Server.Write("\r{0}", "Importing Chair Info...Finished" + Environment.NewLine);
        }
        //-----------------------
        // Private members
        //-----------------------

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
        //-----------------------
        // Properties
        //-----------------------
    }
}
