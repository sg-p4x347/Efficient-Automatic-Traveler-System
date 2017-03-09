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

namespace Efficient_Automatic_Traveler_System
{
    interface IOrderManager
    {
        
        //void ImportOrders(ref List<Order> newOrders, ref OdbcConnection MAS);
        // Returns the order with the specified order number
        Order FindOrder(string orderNo);
        // Returns the entire list of orders
        List<Order> GetOrders
        {
            get;
        }
    }
    class OrderManager : IOrderManager
    {
        #region Public Methods
        public OrderManager()
        {
            m_orders = new List<Order>();
        }
        // Imports and stores all open orders that have not already been stored
        public void ImportOrders(ref List<Order> newOrders, ref OdbcConnection MAS)
        {
            try
            {
                // first clear what is in memory
                m_orders.Clear();
                // next, load the orders that have travelers from the json file
                ImportStoredOrders();

                Server.WriteLine("Importing orders...");
                List<string> currentOrderNumbers = new List<string>();
                // get informatino from header
                if (MAS.State != System.Data.ConnectionState.Open) throw new Exception("MAS is in a closed state!");
                OdbcCommand command = MAS.CreateCommand();
                command.CommandText = "SELECT SalesOrderNo, CustomerNo, ShipVia, OrderDate, ShipExpireDate FROM SO_SalesOrderHeader";
                OdbcDataReader reader = command.ExecuteReader();
                // read info
                int max = 20;
                while (reader.Read() && max > 0)
                {
                    max--;
                    string salesOrderNo = reader.GetString(0);
                    currentOrderNumbers.Add(salesOrderNo);
                    int index = m_orders.FindIndex(x => x.SalesOrderNo == salesOrderNo);

                    // does not match any stored records
                    if (index == -1)
                    {
                        // create a new order
                        Order order = new Order();
                        if (!reader.IsDBNull(0)) order.SalesOrderNo = reader.GetString(0);
                        if (!reader.IsDBNull(1)) order.CustomerNo = reader.GetString(1);
                        if (!reader.IsDBNull(2)) order.ShipVia = reader.GetString(2);
                        if (order.ShipVia == null) order.ShipVia = ""; // havent found a shipper yet, will be LTL regardless
                        if (!reader.IsDBNull(3)) order.OrderDate = reader.GetDateTime(3);
                        if (!reader.IsDBNull(4)) order.ShipDate = reader.GetDateTime(4);
                        // get information from detail
                        if (MAS.State != System.Data.ConnectionState.Open) throw new Exception("MAS is in a closed state!");
                        OdbcCommand detailCommand = MAS.CreateCommand();
                        detailCommand.CommandText = "SELECT ItemCode, QuantityOrdered, UnitOfMeasure FROM SO_SalesOrderDetail WHERE SalesOrderNo = '" + reader.GetString(0) + "'";
                        OdbcDataReader detailReader = detailCommand.ExecuteReader();

                        // Read each line of the Sales Order, looking for the base Table, Chair, ect items, ignoring kits
                        while (detailReader.Read())
                        {
                            string billCode = detailReader.GetString(0);
                            if (!detailReader.IsDBNull(2) && detailReader.GetString(2) != "KIT")
                            {
                                OrderItem item = new OrderItem();
                                if (!detailReader.IsDBNull(0)) item.ItemCode = detailReader.GetString(0);  // itemCode
                                if (!detailReader.IsDBNull(1)) item.QtyOrdered = Convert.ToInt32(detailReader.GetValue(1)); // Quantity
                                order.Items.Add(item);
                            }
                        }
                        detailReader.Close();
                        newOrders.Add(order);
                    }
                    // Update information for existing order
                    else
                    {
                        if (!reader.IsDBNull(1)) m_orders[index].CustomerNo = reader.GetString(1);
                        if (!reader.IsDBNull(2)) m_orders[index].ShipVia = reader.GetString(2);
                        if (m_orders[index].ShipVia == null) m_orders[index].ShipVia = ""; // havent found a shipper yet, will be LTL regardless
                        if (!reader.IsDBNull(3)) m_orders[index].OrderDate = reader.GetDateTime(3);
                        if (!reader.IsDBNull(4)) m_orders[index].ShipDate = reader.GetDateTime(4);
                    }
                }
                reader.Close();
                // cull orders that do not exist anymore
                List<Order> preCullList = new List<Order>(m_orders);
                m_orders.Clear();
                foreach (Order order in preCullList)
                {
                    if (currentOrderNumbers.Exists(x => x == order.SalesOrderNo))
                    {
                        // phew! the order is still here
                        m_orders.Add(order);
                    }
                }
                m_orders.AddRange(newOrders);
            }
            catch (Exception ex)
            {
                Server.WriteLine("Problem importing new orders: " + ex.Message + " Stack Trace: " + ex.StackTrace);
            }
        }
        // reserve inventory items under order items by item type (by traveler)
        public void CheckInventory(ITravelerManager travelerManager, ref OdbcConnection MAS)
        {
            try
            {
                foreach (Traveler traveler in travelerManager.GetTravelers)
                {
                    if (MAS.State != System.Data.ConnectionState.Open) throw new Exception("MAS is in a closed state!");
                    OdbcCommand command = MAS.CreateCommand();
                    command.CommandText = "SELECT QuantityOnSalesOrder, QuantityOnHand FROM IM_ItemWarehouse WHERE ItemCode = '" + traveler.ItemCode + "'";
                    OdbcDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        int onHand = Convert.ToInt32(reader.GetValue(1));
                        // adjust the quantity on hand for orders
                        List<Order> parentOrders = new List<Order>();
                        foreach (string orderNo in traveler.ParentOrders)
                        {
                            parentOrders.Add(FindOrder(orderNo));
                        }
                        parentOrders.Sort((a, b) => b.OrderDate.CompareTo(a.OrderDate)); // sort in descending order (oldest first)
                        for (int i = 0; i < parentOrders.Count && onHand > 0; i++)
                        {
                            Order order = parentOrders[i];
                            foreach (OrderItem item in order.Items)
                            {
                                if (item.ChildTraveler == traveler.ID)
                                {
                                    item.QtyOnHand = Math.Min(onHand, item.QtyOrdered);
                                    onHand -= item.QtyOnHand;
                                }
                            }
                        }
                    }
                    reader.Close();
                }
                
            }
            catch (Exception ex)
            {
                Server.WriteLine("Problem checking order items against inventory: " + ex.Message + " Stack Trace: " + ex.StackTrace);
            }
        }
        // Writes the orders to the json database
        public void BackupOrders()
        {
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string contents = "";
            foreach (Order order in m_orders)
            {
                contents += order.Export();

            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(exeDir, "orders.json"), contents);
        }
        #endregion
        //--------------------------------------------
        #region Interface

        public Order FindOrder(string orderNo)
        {
            return m_orders.Find(x => x.SalesOrderNo == orderNo);
        }
        public List<Order> GetOrders
        {
            get
            {
                return m_orders;
            }
        }
        #endregion
        //--------------------------------------------
        #region Private Methods
        // Imports orders that have been stored
        private void ImportStoredOrders()
        {
            // create the file if it doesn't exist
            StreamWriter w = File.AppendText("orders.json");
            w.Close();
            // open the file
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string line;
            System.IO.StreamReader file = new System.IO.StreamReader(System.IO.Path.Combine(exeDir, "orders.json"));
            while ((line = file.ReadLine()) != null && line != "")
            {
                m_orders.Add(new Order(line));
            }
            file.Close();
        }
        
        #endregion
        //--------------------------------------------
        #region Properties
        private List<Order> m_orders;
        #endregion
        //--------------------------------------------
    }
}
