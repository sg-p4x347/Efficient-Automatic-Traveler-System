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
    public interface IOrderManager
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
    public class OrderManager : IManager, IOrderManager
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
                //foreach (Order order in m_orders) { currentOrderNumbers.Add(order.SalesOrderNo); }

                Server.Write("\r{0}","Importing orders...");
                
                // get informatino from header
                if (MAS.State != System.Data.ConnectionState.Open) throw new Exception("MAS is in a closed state!");
                OdbcCommand command = MAS.CreateCommand();
                //command.CommandText = "SELECT SalesOrderNo, CustomerNo, ShipVia, OrderDate, ShipExpireDate, OrderType, OrderStatus FROM SO_SalesOrderHeader";
                command.CommandText = "SELECT * FROM SO_SalesOrderHeader";
                OdbcDataReader reader = command.ExecuteReader();
                // read info
                while (reader.Read())
                {
                    // if order is not a quote or on hold
                    if (Convert.ToString(reader["WarehouseCode"]) == "000" && Convert.ToChar(reader["OrderType"]) != 'Q')
                    {
                        string salesOrderNo = reader["SalesOrderNo"].ToString();
                        currentOrderNumbers.Add(salesOrderNo);
                        Order order = m_orders.Find(x => x.SalesOrderNo == salesOrderNo);

                        // does not match any stored records
                        if (order == null)
                        {
                            // create a new order
                            order = new Order();
                            order.SalesOrderNo = salesOrderNo;
                            m_orders.Add(order);
                        }
                        // Update information for existing order
                        order.CustomerNo = reader["CustomerNo"].ToString();
                        order.OrderDate = Convert.ToDateTime(reader["OrderDate"]);
                        order.ShipVia = reader["ShipVia"].ToString();
                        if (order.ShipVia == null) order.ShipVia = ""; // havent found a shipper yet, will be LTL regardless
                        order.ShipDate = Convert.ToDateTime(reader["ShipExpireDate"]);
                        if (order.Status != OrderStatus.Removed) order.SetStatus(Convert.ToChar(reader["OrderStatus"]));

                        // get information from detail
                        if (MAS.State != System.Data.ConnectionState.Open) throw new Exception("MAS is in a closed state!");
                        OdbcCommand detailCommand = MAS.CreateCommand();
                        //detailCommand.CommandText = "SELECT ItemCode, QuantityOrdered, UnitOfMeasure, LineKey, QuantityShipped, ExplodedKitItem FROM SO_SalesOrderDetail WHERE SalesOrderNo = '" + reader.GetString(0) + "'";
                        detailCommand.CommandText = "SELECT * FROM SO_SalesOrderDetail WHERE SalesOrderNo = '" + reader.GetString(0) + "'";
                        OdbcDataReader detailReader = detailCommand.ExecuteReader();

                        // Read each line of the Sales Order, looking for the base Table, Chair, ect items, ignoring kits
                        while (detailReader.Read())
                        {
                            string billCode = detailReader["ItemCode"].ToString();
                            if (detailReader["ExplodedKitItem"].ToString() == "N" && detailReader["WarehouseCode"].ToString() == "000")
                            {
                                OrderItem item = order.Items.Find(x => x.LineNo == Convert.ToInt32(detailReader["LineKey"]));
                                /* IF using EATS inventory management, each order item needs to be pre-allocated when the order comes in, but assigned and closed when
                                 * items are produced and get a label with a specific order on them
                                 * 
                                 * IF using MAS inventory managemetn, each order item gets a pre-allocated quantity from inventory, but item assignment to the order when
                                 * items are completed can be ambiguous when it comes time to ship
                                 * 
                                 * EX:
                                 * DAY 1.) 1 in inventory, an order for 3 and an order for 2
                                 * DAY 2.) 3 in inventory, either order can ship, but when one is chosen to ship, the other one cannot.
                                 */

                                if (item == null)
                                {
                                    // A new item
                                    item = new OrderItem(order);
                                    // a new item, a new traveler
                                    item.ItemCode = detailReader["ItemCode"].ToString();  // itemCode
                                    item.LineNo = Convert.ToInt32(detailReader["LineKey"]);
                                    // allocate inventory items to this order item
                                    // AllocateOrderItem(item, ref MAS);
                                    order.Items.Add(item);
                                }
                                // Update fields
                                item.QtyOrdered = Convert.ToInt32(detailReader["QuantityOrdered"]); // Ordered Quantity
                                item.QtyShipped = Convert.ToInt32(detailReader["QuantityShipped"]); // Shipped Qty
                            }
                        }
                        detailReader.Close();
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
                    } else
                    {
                        var test = "ded";
                    }
                }
                // allocate teh inventory
                AllocateCurrentInventoryForCurrentOrders(MAS);

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

                // EATS INVENTORY ALLOCATION
                int totalAllocated = 0;
                //totalAllocated= m_orders.Sum(order => order.Items.Where(item => item.ItemCode == orderItem.ItemCode).Sum(item => item.QtyOnHand));

                
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
                        // MAS INVENTORY Allocation
                        totalAllocated = Convert.ToInt32(reader.GetValue(0));
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
        public void AllocateCurrentInventoryForCurrentOrders(OdbcConnection MAS)
        {
            Dictionary<string, List<OrderItem>> ordersByItemCode = new Dictionary<string, List<OrderItem>>();
            // get all orderItems in one list
            List<OrderItem> allItems = m_orders.SelectMany(o => o.Items).ToList();
            // get all distinct itemCodes on order
            List<string> itemCodes = allItems.Select(i => i.ItemCode).Distinct().ToList();
            // for all itemCodes on order
            foreach (string itemCode in itemCodes)
            {
                // Get the number of available [itemCode] items in MAS inventory
                int available = 0;
                if (MAS.State != System.Data.ConnectionState.Open) throw new Exception("MAS is in a closed state!");
                OdbcCommand command = MAS.CreateCommand();
                command.CommandText = "SELECT QuantityOnSalesOrder, QuantityOnHand, QuantityOnBackOrder FROM IM_ItemWarehouse WHERE ItemCode = '" + itemCode + "'";
                OdbcDataReader reader = command.ExecuteReader();
                int SOqty = 0;
                if (reader.Read())
                {
                    // avialable = onHand
                    available = Convert.ToInt32(reader.GetValue(1));
                    SOqty = Convert.ToInt32(reader.GetValue(0)) + Convert.ToInt32(reader.GetValue(2));
                }
                reader.Close();
                int qtyOnSO = 0;
                // get all Open OrderItem(s) with this itemCode
                List<OrderItem> items = allItems.Where(x => x.ItemCode == itemCode).ToList();
                // add non open orders' quantities to available
                //available += items.Where(i => i.Parent.Status != OrderStatus.Open || i.ItemStatus != OrderStatus.Open).Sum(j => j.QtyNeeded);
                // remove all non-open items
                items.RemoveAll(i => i.Parent.Status != OrderStatus.Open);
                // sort the list in ascending order with respect to the ship date
                items.Sort((i, j) => i.Parent.ShipDate.CompareTo(j.Parent.ShipDate));

                // for each OrderItem that has this itemCode;
                foreach (OrderItem item in items)
                {
                    qtyOnSO += item.QtyNeeded;
                    
                    // allocate as much as possible to this OrderItem
                    item.QtyOnHand = Math.Min(item.QtyNeeded, available);
                    
                    // subtract from the avilable supply
                    available -= item.QtyOnHand;
                }
                
                if (SOqty != qtyOnSO)
                {
                    Server.WriteLine("MAS inventory inconsistency for " + itemCode + " : " + qtyOnSO + " " + SOqty + items.Select(i => i.Parent.SalesOrderNo + " " + i.QtyOrdered).ToList().Stringify(false) );
                }
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
        
        public void NotifyShipDates()
        {
            string message = "";
            bool notify = false;
            TimeSpan notifyWithin  = new TimeSpan(3, 0, 0, 0);

            foreach (Order order in m_orders)
            {
                TimeSpan timeUntil = order.ShipDate - DateTime.Today;
                if (timeUntil < notifyWithin)
                {
                    
                    message += order.SalesOrderNo + "\tShips in " + timeUntil.Days + " days : " + order.ShipDate.ToString("MM/dd/yyyy") + Environment.NewLine;
                    List<OrderItem> travelerItems = order.Items.Where(i => i.ChildTraveler != -1).ToList();
                    if (travelerItems.Count > 0) {
                        notify = true;
                        message += "\tTravelers:" + Environment.NewLine;
                        foreach (OrderItem item in travelerItems)
                        {
                            message += "\t\t" + item.ChildTraveler.ToString() + "\t; " + item.ItemCode + "\t; " + item.QtyNeeded + " need to ship" + Environment.NewLine;
                        }
                    }
                    message += "".PadLeft(50, '_') + Environment.NewLine;
                }
            }
            if (notify)
            {
                Server.NotificationManager.PushNotification("Close Ship Dates", message);
            }
        }
        // remove this order's ability to influnce travelers
        public void RemoveOrder(Order order)
        {
            // remove traveler dependencies
            foreach (OrderItem item in order.Items.Where(i => i.ChildTraveler >= 0))
            {
                
                Traveler child = Server.TravelerManager.FindTraveler(item.ChildTraveler);
                if (child != null) RemoveOrder(order, child);
            }
            Backup();
        }
        public void AddOrder(Order order)
        {
            // open all the items back up again
            foreach (OrderItem item in order.Items)
            {
                item.ItemStatus = OrderStatus.Open;
            }
            Backup();
        }
        public void RefactorOrders()
        {
            OdbcConnection MAS = new OdbcConnection();
            MAS.ConnectionString = "DSN=SOTAMAS90;Company=MGI;UID=GKC;PWD=sgp4x347;";
            MAS.Open();
            ImportOrders(ref MAS);
            Backup();
            Server.TravelerManager.Backup();
        }
        // Just remove this order from this traveler
        public void RemoveOrder(Order order, Traveler child)
        {
            foreach (OrderItem item in order.FindItems(child.ID))
            {
                item.ItemStatus = OrderStatus.Removed;
                child.ParentOrders.Remove(order);
                child.ParentOrderNums.Remove(order.SalesOrderNo);

                item.ChildTraveler = -1;
            }
            Backup();
            Server.TravelerManager.Backup();
        }
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
                foreach (OrderItem item in order.FindItems(traveler.ID))
                {
                    item.ChildTraveler = -1;
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
                if (order.Status != OrderStatus.Closed)
                {
                    List<OrderItem> items = new List<OrderItem>();
                    foreach(OrderItem item in order.Items)
                    {
                        // only import items that have a child traveler
                        //if (item.ChildTraveler >= 0 && item.QtyOnHand < item.QtyNeeded)
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
