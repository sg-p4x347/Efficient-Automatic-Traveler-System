#define NewOrders
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
        // removes all occurences of the specified traveler from order items
        void ReleaseTraveler(Traveler traveler);

    }
    class OrderManager : IManager, IOrderManager
    {
        #region Public Methods
        public OrderManager()
        {
            m_orders = new List<Order>();
        }
        // Imports and stores all open orders that have not already been stored
        public void ImportOrders(ref OdbcConnection MAS)
        {
            try
            {
                // load the orders that have travelers from the json file
                Import();
                List<string> currentOrderNumbers = new List<string>();
                foreach (Order order in m_orders) { currentOrderNumbers.Add(order.SalesOrderNo); }

                Server.Write("\r{0}","Importing orders...");
                
                // get informatino from header
                if (MAS.State != System.Data.ConnectionState.Open) throw new Exception("MAS is in a closed state!");
                OdbcCommand command = MAS.CreateCommand();
                command.CommandText = "SELECT SalesOrderNo, CustomerNo, ShipVia, OrderDate, ShipExpireDate FROM SO_SalesOrderHeader";
                OdbcDataReader reader = command.ExecuteReader();
                // read info
                while (reader.Read())
                {
                    string salesOrderNo = reader.GetString(0);
                    currentOrderNumbers.Add(salesOrderNo);
                    Order order = m_orders.Find(x => x.SalesOrderNo == salesOrderNo);

                    // does not match any stored records
                    if (order == null)
                    {
                        // create a new order
                        order = new Order();
                        if (!reader.IsDBNull(0)) order.SalesOrderNo = reader.GetString(0);
                        if (!reader.IsDBNull(1)) order.CustomerNo = reader.GetString(1);
                        if (!reader.IsDBNull(2)) order.ShipVia = reader.GetString(2);
                        if (order.ShipVia == null) order.ShipVia = ""; // havent found a shipper yet, will be LTL regardless
                        if (!reader.IsDBNull(3)) order.OrderDate = reader.GetDateTime(3);
                        if (!reader.IsDBNull(4)) order.ShipDate = reader.GetDateTime(4);
                        
#if NewOrders
                        m_orders.Add(order);
#endif
                    }
                    // Update information for existing order
                    else
                    {
                        if (!reader.IsDBNull(1)) order.CustomerNo = reader.GetString(1);
                        if (!reader.IsDBNull(2)) order.ShipVia = reader.GetString(2);
                        if (order.ShipVia == null) order.ShipVia = ""; // havent found a shipper yet, will be LTL regardless
                        if (!reader.IsDBNull(3)) order.OrderDate = reader.GetDateTime(3);
                        if (!reader.IsDBNull(4)) order.ShipDate = reader.GetDateTime(4);
                    }

                    // get information from detail
                    if (MAS.State != System.Data.ConnectionState.Open) throw new Exception("MAS is in a closed state!");
                    OdbcCommand detailCommand = MAS.CreateCommand();
                    detailCommand.CommandText = "SELECT ItemCode, QuantityOrdered, UnitOfMeasure, LineKey FROM SO_SalesOrderDetail WHERE SalesOrderNo = '" + reader.GetString(0) + "'";
                    OdbcDataReader detailReader = detailCommand.ExecuteReader();

                    // Read each line of the Sales Order, looking for the base Table, Chair, ect items, ignoring kits
                    while (detailReader.Read())
                    {
                        string billCode = detailReader.GetString(0);
                        if (!detailReader.IsDBNull(2) && detailReader.GetString(2) != "KIT")
                        {
                            OrderItem item = order.Items.Find(x => x.LineNo == Convert.ToInt32(detailReader.GetInt32(3)));
                            if (item == null)
                            {
                                item = new OrderItem();
                                // a new item, a new traveler
                                if (!detailReader.IsDBNull(0)) item.ItemCode = detailReader.GetString(0);  // itemCode
                                if (!detailReader.IsDBNull(1)) item.QtyOrdered = Convert.ToInt32(detailReader.GetValue(1)); // Quantity
                                item.LineNo = Convert.ToInt32(detailReader.GetInt32(3));
                                // allocate inventory items to this order item
                                AllocateOrderItem(item, ref MAS);

                                order.Items.Add(item);
                            }
                        }
                    }
                    detailReader.Close();
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
                    } else
                    {

                    }
                }
                Server.Write("\r{0}", "Importing orders...Finished\n");
            }
            catch (Exception ex)
            {
                Server.Write("\r{0}", "Importing orders...Failed\n");
                Server.WriteLine("Problem importing new orders: " + ex.Message + " Stack Trace: " + ex.StackTrace);
            }
        }
        // reserve inventory items under order items by item type (by traveler)
        public void AllocateOrderItem(OrderItem orderItem, ref OdbcConnection MAS)
        {
            try
            {
                /* get total that is allocated for orders (items leave allocation when orders are invoiced,
                 which is also when orders move to a "Closed" state and aren't brought back into memory*/

                int totalAllocated = m_orders.Sum(order => order.Items.Where(item => item.ItemCode == orderItem.ItemCode).Sum(item => item.QtyOnHand));
                int onHand = 0;
                if (Convert.ToBoolean(ConfigManager.Get("useMASinventory")))
                {
                    if (MAS.State != System.Data.ConnectionState.Open) throw new Exception("MAS is in a closed state!");
                    OdbcCommand command = MAS.CreateCommand();
                    command.CommandText = "SELECT QuantityOnSalesOrder, QuantityOnHand FROM IM_ItemWarehouse WHERE ItemCode = '" + orderItem.ItemCode + "'";
                    OdbcDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        onHand = Convert.ToInt32(reader.GetValue(1));
                    }
                    reader.Close();
                } else
                {
                    onHand = InventoryManager.Get(orderItem.ItemCode);
                }

                orderItem.QtyOnHand = Math.Min(orderItem.QtyOrdered, Math.Max(onHand - totalAllocated, 0));
            }
            catch (Exception ex)
            {
                Server.WriteLine("Problem checking order items against inventory on order: " + ex.Message + " Stack Trace: " + ex.StackTrace);
            }
        }
        // reserve inventory items under order items by item type (by traveler)
        //public void CheckInventory(ITravelerManager travelerManager, ref OdbcConnection MAS)
        //{
        //    try
        //    {
        //        foreach (Traveler traveler in travelerManager.GetTravelers)
        //        {
        //            if (MAS.State != System.Data.ConnectionState.Open) throw new Exception("MAS is in a closed state!");
        //            OdbcCommand command = MAS.CreateCommand();
        //            command.CommandText = "SELECT QuantityOnSalesOrder, QuantityOnHand FROM IM_ItemWarehouse WHERE ItemCode = '" + traveler.ItemCode + "'";
        //            OdbcDataReader reader = command.ExecuteReader();
        //            if (reader.Read())
        //            {
        //                int onHand = Convert.ToInt32(reader.GetValue(1));
        //                // adjust the quantity on hand for orders
        //                List<Order> parentOrders = new List<Order>();
        //                foreach (string orderNo in traveler.ParentOrders)
        //                {
        //                    Order parentOrder = FindOrder(orderNo);
        //                    if (parentOrder != null)
        //                    {
        //                        parentOrders.Add(parentOrder);
        //                    }
        //                }
        //                // remove orders that no longer exisst
        //                traveler.ParentOrders.RemoveAll(x => !parentOrders.Exists(y => y.SalesOrderNo == x));
        //                parentOrders.Sort((a, b) => a.ShipDate.CompareTo(b.ShipDate)); // sort in ascending order (soonest first)

        //                for (int i = 0; i < parentOrders.Count && onHand > 0; i++)
        //                {
        //                    Order order = parentOrders[i];
        //                    foreach (OrderItem item in order.Items)
        //                    {
        //                        if (item.ChildTraveler == traveler.ID)
        //                        {
        //                            item.QtyOnHand = Math.Min(onHand, item.QtyOrdered);
        //                            onHand -= item.QtyOnHand;
        //                        }
        //                    }
        //                }
        //            }
        //            reader.Close();
        //        }
                
        //    }
        //    catch (Exception ex)
        //    {
        //        Server.WriteLine("Problem checking order items against inventory: " + ex.Message + " Stack Trace: " + ex.StackTrace);
        //    }
        //}
        

        #endregion
        //--------------------------------------------
        #region IOrderManager

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
        public void ReleaseTraveler(Traveler traveler)
        {
            // iterate over all applicable orders
            foreach (string orderNo in traveler.ParentOrderNums)
            {
                Order order = FindOrder(orderNo);
                // for each item in the order
                foreach (OrderItem item in order.Items)
                {
                    if (item.ChildTraveler == traveler.ID)
                    {
                        item.ChildTraveler = -1;
                    }
                }
            }
        }
        #endregion
        //--------------------------------------------
        #region IManager

        public void Import(DateTime? date = null)
        {
            m_orders.Clear();
            if (BackupManager.CurrentBackupExists("orders.json") || date != null)
            {
                List<string> orderArray = (new StringStream(BackupManager.Import("orders.json", date))).ParseJSONarray();
                Server.Write("\r{0}", "Loading orders from backup...");
                foreach (string orderJSON in orderArray)
                {
                    m_orders.Add(new Order(orderJSON));
                }
                Server.Write("\r{0}", "Loading orders from backup...Finished\n");
            } else
            {
                ImportPast();
            }
        }
        public void ImportPast()
        {
            m_orders.Clear();
            List<string> orderArray = (new StringStream(BackupManager.ImportPast("orders.json"))).ParseJSONarray();
            foreach (string orderJSON in orderArray)
            {
                Order order = new Order(orderJSON);
                // add this order to the master list if it is not closed
                if (order.State != OrderState.Closed)
                {
                    List<OrderItem> items = new List<OrderItem>();
                    foreach(OrderItem item in order.Items)
                    {
                        // only import items that need production
                        if (item.QtyOnHand < item.QtyOrdered)
                        {
                            items.Add(item);
                        }
                    }
                    order.Items = items;
                    m_orders.Add(order);
                }
            }
        }
        public void Backup()
        {
            BackupManager.Backup("orders.json", m_orders.Stringify<Order>(false,true));
        }

        #endregion
        //--------------------------------------------
        #region Private Methods

        #endregion
        //--------------------------------------------
        #region Properties
        private List<Order> m_orders;
        #endregion
        //--------------------------------------------
    }
}
