
#define Labels
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data.Odbc;
using System.Net;
using System.Net.Http;

namespace Efficient_Automatic_Traveler_System
{
    enum TravelerEvent
    {
        Completed,
        Scrapped,
        Reworked,
        Moved
    }
    enum LabelType
    {
        Tracking,
        Scrap,
        Pack,
        Table,
        Test
    }
    class Event : IEquatable<Event>
    {
        public Event() { }
        public Event (string json)
        {
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                type = (TravelerEvent)Enum.Parse(typeof(TravelerEvent), obj["type"]);
                date = obj["date"];
                time = Convert.ToDouble(obj["time"]);
                station = Convert.ToInt32(obj["station"]);
            }
            catch (Exception ex)
            {
                Server.WriteLine("Problem when reading event from file: " + ex.Message + "; StackTrace: " + ex.StackTrace);
            }
        }
        public Event (TravelerEvent e, double t, int s)
        {
            type = e;
            time = t;
            station = s;
            date = DateTime.Now.ToString("MM/dd/yy @ hh:mm");
        }
        public override string ToString()
        {
            string json = "";
            json += "{";
            json += "\"type\":" + type.ToString("d") + ",";
            json += "\"date\":" + '"' + date + '"' + ",";
            json += "\"time\":" + time + ",";
            json += "\"station\":" + station;
            json += "}";
            return json;
        }
        public static bool operator ==(Event A, Event B)
        {
            return (A.type == B.type && A.time == B.time && A.station == B.station && A.date == B.date);
        }
        public bool Equals(Event B)
        {
            return (type == B.type && time == B.time && station == B.station && date == B.date);
        }
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }
        public static bool operator !=(Event A, Event B)
        {
            return !(A.type == B.type && A.time == B.time && A.station == B.station && A.date == B.date);
        }
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = type.GetHashCode();
                hashCode = (hashCode * 397) ^ time.GetHashCode();
                hashCode = (hashCode * 397) ^ station.GetHashCode();
                hashCode = (hashCode * 397) ^ date.GetHashCode();
                return hashCode;
            }
        }
        public TravelerEvent type;
        public double time;
        public int station;
        public string date;
    }
    struct NameValueQty<valueType,qtyType>
    {
        public NameValueQty(string name, valueType value, qtyType qty)
        {
            Name = name;
            Value = value;
            Qty = qty;
        }
        public override string ToString()
        {
            string json = "";
            json += '{';
            json += "\"name\":" + '"' + Name.Replace("\"","\\\"") + '"' + ',';
            json += "\"value\":" + '"' + Value.ToString().Replace("\"", "\\\"") + '"' + ',';
            json += "\"qty\":" + '"' + Qty.ToString().Replace("\"", "\\\"") + '"';
            json += '}';
            return json;
        }
        public string Name;
        public valueType Value;
        public qtyType Qty;
    }
    
    abstract class Traveler
    {
        #region Public Methods
        public Traveler() { }
        // Gets the base properties and orders of the traveler from a json string
        public Traveler(string json)
        {
            Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
            m_ID = Convert.ToInt32(obj["ID"]);
            
            m_quantity = Convert.ToInt32(obj["quantity"]);
            m_part = new Bill(obj["itemCode"], m_quantity);
            Items = new List<TravelerItem>();
            foreach (string item in (new StringStream(obj["items"])).ParseJSONarray())
            {
                Items.Add(new TravelerItem(item));
            }
            m_parentOrders = (new StringStream(obj["parentOrders"])).ParseJSONarray();
            m_station = StationClass.GetStation(obj["station"]);
        }
        // Creates a traveler from a part number and quantity, then loads the bill of materials
        //public Traveler(string billNo, int quantity, ref OdbcConnection MAS)
        //{
        //    // set META information
        //    m_part = new Bill(billNo, quantity, ref MAS);
        //    m_quantity = quantity;
        //    NewID();

        //    // Import the part
        //    ImportPart(ref MAS);
        //}
        public Traveler(string billNo, int quantity)
        {
            // set META information
            m_part = new Bill(billNo, quantity);
            m_quantity = quantity;
            m_parentOrders = new List<string>();
            Station = StationClass.GetStation("Start");
            Items = new List<TravelerItem>();
            NewID();
        }
        public virtual void ImportPart(IOrderManager orderManager, ref OdbcConnection MAS)
        {
            m_part = new Bill(m_part.BillNo, m_quantity, ref MAS);
        }
        public void NewID()
        {
            // open the currentID.txt file
            //string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            //System.IO.StreamReader readID = new StreamReader(System.IO.Path.Combine(exeDir, "currentID.txt"));
            //m_ID = Convert.ToInt32(readID.ReadLine());
            m_ID = Convert.ToInt32(ConfigManager.Get("nextTravelerID"));
            ConfigManager.Set("nextTravelerID", (m_ID + 1).ToString());
            //readID.Close();
            // increment the current ID
            //File.WriteAllText(System.IO.Path.Combine(exeDir, "currentID.txt"), (m_ID + 1).ToString() + '\n');
        }
        // Finds all the components in the top level bill, setting key components along the way
        //public void FindComponents(Bill bill)
        //{
        //    // find work and or material
        //    foreach (Item componentItem in bill.ComponentItems)
        //    {
        //        // update the component's total quantity
        //        componentItem.TotalQuantity = bill.TotalQuantity * componentItem.QuantityPerBill;
        //        // sort out key components
        //        string itemCode = componentItem.ItemCode;
        //        if (itemCode == "/LWKE1" || itemCode == "/LWKE2" || itemCode == "/LCNC1" || itemCode == "/LCNC2")
        //        {
        //            // CNC labor
        //            if (m_cnc == null)
        //            {
        //                m_cnc = componentItem;
        //            } else
        //            {
        //                m_cnc.TotalQuantity += componentItem.TotalQuantity;
        //            }
        //        }
        //        else if (itemCode == "/LBND2" || itemCode == "/LBND3")
        //        {
        //            // Straight Edgebander labor
        //            if (m_ebander == null)
        //            {
        //                m_ebander = componentItem;
        //            } else
        //            {
        //                m_ebander.TotalQuantity += componentItem.TotalQuantity;
        //            }
        //        }
        //        else if (itemCode == "/LPNL1" || itemCode == "/LPNL2")
        //        {
        //            // Panel Saw labor
        //            if (m_saw == null)
        //            {
        //                m_saw = componentItem;
        //            } else
        //            {
        //                m_saw.TotalQuantity += componentItem.TotalQuantity;
        //            }
        //        }
        //        else if (itemCode == "/LCEB1" | itemCode == "/LCEB2")
        //        {
        //            // Contour Edge Bander labor (vector)
        //            if (m_vector == null)
        //            {
        //                m_vector = componentItem;
        //            } else
        //            {
        //                m_vector.TotalQuantity += componentItem.TotalQuantity;
        //            }
        //        }
        //        else if ( itemCode == "/LATB1" || itemCode == "/LATB2" || itemCode == "/LATB3" || itemCode == "/LACH1" || itemCode == "/LACH2" || itemCode == "/LACH3")
        //        {
        //            // Assembly labor
        //            if (m_assm == null)
        //            {
        //                m_assm = componentItem;
        //            } else
        //            {
        //                m_assm.TotalQuantity += componentItem.TotalQuantity;
        //            }
        //        }
        //        else if (itemCode == "/LBOX1")
        //        {
        //            // Box construction labor
        //            if (m_box == null)
        //            {
        //                m_box = componentItem;
        //            } else
        //            {
        //                m_box.TotalQuantity += componentItem.TotalQuantity;
        //            }
        //        }
        //        else if (itemCode.Substring(0, 3) == "006")
        //        {
        //            // Material
        //            if (m_material == null)
        //            {
        //                m_material = componentItem;
        //            } else
        //            {
        //                m_material.TotalQuantity += componentItem.TotalQuantity;
        //            }
        //        }
        //        else if (itemCode.Substring(0, 2) == "87")
        //        {
        //            // Edgeband
        //            if (m_eband == null)
        //            {
        //                m_eband = componentItem;
        //            } else
        //            {
        //                m_eband.TotalQuantity += componentItem.TotalQuantity;
        //            }
        //        }
        //        else if (m_box == null && itemCode.Substring(0, 2) == "90")
        //        {
        //            // Paid for box
        //            m_boxItemCode = itemCode;
        //        }
        //        else
        //        {
        //            // anything else
        //            // check the blacklist
        //            bool blacklisted = false;
        //            foreach (BlacklistItem blItem in m_blacklist )
        //            {
        //                if (blItem.StartsWith(itemCode))
        //                {
        //                    blacklisted = true;
        //                    break;
        //                }
        //            }
        //            if (!blacklisted)
        //            {
        //                // check for existing item first
        //                bool foundItem = false;
        //                foreach (Item component in m_components)
        //                {
        //                    if (component.ItemCode == itemCode)
        //                    {
        //                        foundItem = true;
        //                        component.TotalQuantity += componentItem.TotalQuantity;
        //                        break;
        //                    }
        //                }
        //                if (!foundItem)
        //                {
        //                    m_components.Add(componentItem);
        //                }
        //            }
        //        }
        //    }
        //    // Go deeper into each component bill
        //    foreach (Bill componentBill in bill.ComponentBills)
        //    {
        //        componentBill.TotalQuantity = bill.TotalQuantity * componentBill.QuantityPerBill;
        //        FindComponents(componentBill);
        //    }
        //}
        //check inventory to see how many actually need to be produced.
        //public void CheckInventory(ref OdbcConnection MAS)
        //{
        //    try
        //    {
        //        if (MAS.State != System.Data.ConnectionState.Open) throw new Exception("MAS is in a closed state!");
        //        OdbcCommand command = MAS.CreateCommand();
        //        command.CommandText = "SELECT QuantityOnSalesOrder, QuantityOnHand FROM IM_ItemWarehouse WHERE ItemCode = '" + m_part.BillNo + "'";
        //        OdbcDataReader reader = command.ExecuteReader();
        //        if (reader.Read())
        //        {
        //            int available = Convert.ToInt32(reader.GetValue(1)) - Convert.ToInt32(reader.GetValue(0));
        //            if (available >= 0)
        //            {
        //                // No parts need to be produced
        //                m_quantity = 0;
        //            }
        //            else
        //            {
        //                // adjust the quantity that needs to be produced
        //                m_quantity = Math.Min(-available, m_quantity);
        //            }
        //        }
        //        reader.Close();
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine("An error occured when accessing inventory: " + ex.Message);
        //    }
        //}
        // returns a JSON formatted string containing traveler information
        public override string ToString()
        {
            string json = "";
            json += "{";
            // BASIC PROPERTIES
            json += "\"ID\":" + m_ID + ",";
            json += "\"itemCode\":" + '"' + m_part.BillNo + '"' + ",";
            json += "\"quantity\":" + m_quantity + ",";
            // ITEMS [...]
            json += "\"items\":" + Items.Stringify<TravelerItem>() + ',';
            // PARENT ORDERS [...]
            json += "\"parentOrders\":" + m_parentOrders.Stringify<string>() + ',';
            // UNIFIED STATION
            json += "\"station\":" + '"' + StationClass.GetStationName(Station) + '"';
            // packs in members specific to derived classes
            json += ExportProperties(); 

            json += "}\n";
            return json;
        }
        // print a label for this traveler
        public bool PrintLabel(ushort itemID, LabelType type, int qty = 1)
        {
            try
            {
                string result = "";
                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.ContentType] = "application/json";
                    string json = "{";
                    string fields = GetLabelFields(itemID, type);
                    string printer = "";
                    string template = "";
                    // TEMP
                    //type = LabelType.Test;
                    switch (type)
                    {
                        case LabelType.Tracking:    template = "4x2 Table Travel1";     printer = "4x2IT"; break; // 4x2Pack --> in hall
                        case LabelType.Scrap:       template = "4x2 Table Scrap1";      printer = "4x2IT"; break;
                        case LabelType.Pack:        template = "4x2 Table Carton EATS"; printer = "4x2IT"; break;
                        case LabelType.Table:       template = "4x6 Table EATS";        printer = "4x6Table"; break;
                        case LabelType.Test:        template = "4x2 Table Carton EATS logo"; printer = "4x2IT"; break;
                    }

                    // piecing it together
                    json += fields.Trim(',');
                    json += ",\"printer\":\"" + printer + "\"";
                    json += ",\"template\":\"" + template + "\"";
                    json += ",\"qty\":" + qty;
                    json += '}';
#if Labels
                    Dictionary < string, string> labelConfigs = (new StringStream(ConfigManager.Get("print"))).ParseJSON();
                    // only print if the config says so
                    if (labelConfigs.ContainsKey(type.ToString()) && Convert.ToBoolean(labelConfigs[type.ToString()]))
                    {
                        result = client.UploadString(@"http://192.168.2.6:8080/printLabel", "POST", json);
                        return result == "Label Printed";
                    }
                    
#endif
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Traveler [" + ID + "] could not print a label: " + ex.Message); 
            }
            return false;
        }
        // print a traveler pack label
        public abstract string GetLabelFields(ushort itemID, LabelType type);

        public static bool IsTable(string s)
        {
            return s != null && ((s.Length == 9 && s.Substring(0, 2) == "MG") || (s.Length == 10 && (s.Substring(0, 3) == "38-" || s.Substring(0, 3) == "41-")));
        }
        public static bool IsChair(string s)
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
        
        // Manually sets the station 
        //public virtual void MoveTo(int station)
        //{
        //    m_station = station;
        //}
        public TravelerItem FindItem(ushort ID)
        {
            return Items.Find(x => x.ID == ID);
        }
        public void ScrapItem(ushort ID)
        {
            FindItem(ID).Scrapped = true;
        }
        
        // advances all completed items at the specified station
        public void Advance(int station)
        {
            foreach (TravelerItem item in Items)
            {
                if (item.Station == station && item.IsComplete())
                {
                    AdvanceItem(item.ID);
                }
            }
        }

        public TravelerItem AddItem(int station)
        {
            // find the highest id
            ushort highestID = 0;
            foreach (TravelerItem item in Items)
            {
                highestID = Math.Max(highestID, item.ID);
            }
            // use the next id (highest + 1)
            TravelerItem newItem = new TravelerItem((ushort)(highestID + 1));
            newItem.Station = station;
            Items.Add(newItem);
            return newItem;
        }
        public int QuantityPendingAt(int station)
        {
            int quantityPending = 0;
            quantityPending += Items.Where(x => x.Station == station && !x.History.Exists(e => e.station == station && e.type == TravelerEvent.Completed)).Count();
            // these stations can create items
            if (StationClass.FindStation(station).CanCreateItems && m_station == station)
            {
                quantityPending = m_quantity - Items.Where(x => !x.Scrapped).Count(); // calculates the total item deficit for this traveler
            }
            return quantityPending;
        }
        public int QuantityAt(int station)
        {
            return Items.Where(x => x.Station == station).Count();
        }
        public int QuantityScrapped()
        {
            return Items.Where(x => x.Scrapped).Count();
        }
        public int QuantityCompleteAt(int station)
        {
            return Items.Where(x => x.Station == station && x.History.Exists(e => e.station == station && e.type == TravelerEvent.Completed)).Count();
        }
        // export for clients to display
        public string Export(string clientType, int station)
        {
            string json = "";
            json += "{";
            json += "\"ID\":" + m_ID + ",";
            json += "\"itemCode\":" + '"' + m_part.BillNo + '"' + ",";
            json += "\"quantity\":" + m_quantity + ",";
            json += "\"items\":" + Items.Stringify() + ',';

            if (clientType == "OperatorClient")
            {
                json += "\"station\":" + station.ToString() + ",";
                json += "\"qtyPending\":" + QuantityPendingAt(station) + ",";
                json += "\"qtyScrapped\":" + QuantityScrapped() + ",";
                json += "\"qtyCompleted\":" + QuantityCompleteAt(station) + ",";
                json += "\"members\":[";
                json += (new NameValueQty<string, string>("Description", m_part.BillDesc, "")).ToString();
                json += ExportTableRows(clientType, station);
                json += "]";
            }
            else if (clientType == "SupervisorClient")
            {
                json += "\"stations\":";
                List<int> stations = new List<int>();
                if (m_station == StationClass.GetStation("Start") || QuantityPendingAt(m_station) > 0 || QuantityAt(m_station) > 0) stations.Add(m_station);
                foreach (TravelerItem item in Items)
                {
                    if (!stations.Exists(x => x == item.Station))
                    {
                        stations.Add(item.Station);
                    }
                }
                json += stations.Stringify();
            }
            else if (clientType == "Raw")
            {
                json += "\"description\":" + m_part.BillDesc.Quotate() + ',';
                json += "\"starting station\":" + m_station;
            }
            json += "}";
            return json;

        }
        // export for JSON viewer
        public string ExportHuman()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"ID",m_ID.ToString() },
                {"Model",m_part.BillNo.Quotate() },
                {"Description",m_part.BillDesc.Quotate() },
                {"Qty on traveler",m_quantity.ToString() },
                {"Orders",m_parentOrders.Stringify() },
                {"Items",Items.Stringify() }
            };
            return obj.Stringify();
        }
        // export for summary view
        public string ExportSummary()
        {
            int qtyPending = m_quantity - Items.Where(x => !x.Scrapped).Count();
            int qtyComplete = QuantityAt(StationClass.GetStation("Finished"));
            int qtyInProcess = Items.Where(x => !x.Scrapped).Count() - qtyComplete;
            // Displays properties in order
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"Traveler",m_ID.ToString() },
                {"Model",m_part.BillNo.Quotate() },
                {"Pending",qtyPending.ToString()},
                {"In process",qtyInProcess.ToString()},
                {"Scrapped",QuantityScrapped().ToString() },
                {"Complete",qtyComplete.ToString() },
                {"Orders",m_parentOrders.Stringify() }
            };
            return obj.Stringify();
        }
        #endregion
        //--------------------------------------------------------
        #region Abstract Methods

        public abstract string ExportTableRows(string clientType, int station);
        // advances the item to the next station
        public abstract void AdvanceItem(ushort ID);
        // gets the next station for the given item
        public abstract int GetNextStation(UInt16 itemID);
#endregion
        //--------------------------------------------------------
        #region Private Methods
        // overridden in derived classes, packs properties into the Export() json string
        protected abstract string ExportProperties();
#endregion
        //--------------------------------------------------------
#region Properties

        // general
        protected int m_ID;
        protected Bill m_part;
        protected int m_quantity;
        private List<TravelerItem> items;
        protected List<string> m_parentOrders;
        private int m_station;

#endregion
        //--------------------------------------------------------
#region Interface
        internal int ID
        {
            get
            {
                return m_ID;
            }
        }
        internal Bill Part
        {
            get
            {
                return m_part;
            }
        }

        internal int Quantity
        {
            get
            {
                return m_quantity;
            }

            set
            {
                m_quantity = value;
                //m_part.TotalQuantity = m_quantity;
                //FindComponents(m_part);
            }
        }
        internal string ItemCode
        {
            get
            {
                return m_part.BillNo;
            }
        }
        internal int Station
        {
            get
            {
                //foreach (TravelerItem item in Items)
                //{
                //    m_station = item.Station;
                //    if (item.Station != Items[0].Station)
                //    {
                //        m_station = -1;
                //        return -1;
                //    }
                //}
                return m_station;
            }
            set
            {
                m_station = value;
            }
        }

        internal List<string> ParentOrders
        {
            get
            {
                return m_parentOrders;
            }

            set
            {
                m_parentOrders = value;
            }
        }

        public List<TravelerItem> Items
        {
            get
            {
                return items;
            }

            set
            {
                items = value;
            }
        }
#endregion
    }
}
