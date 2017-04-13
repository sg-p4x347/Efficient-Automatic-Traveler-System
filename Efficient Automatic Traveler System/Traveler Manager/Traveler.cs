
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data.Odbc;
using System.Net;
using System.Net.Http;
using System.Globalization;

namespace Efficient_Automatic_Traveler_System
{
    enum EventType
    {
        Completed,
        Scrapped,
        Reworked,
        Moved,
        Login,
        Finished
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
                type = (EventType)Enum.Parse(typeof(EventType), obj["type"]);
                date = DateTime.ParseExact(obj["date"],"O", CultureInfo.InvariantCulture);
                time = Convert.ToDouble(obj["time"]);
                station = StationClass.GetStation(obj["station"]);
                user = UserManager.Find( obj["userID"]);
            }
            catch (Exception ex)
            {
                Server.WriteLine("Problem when reading event from file: " + ex.Message + "; StackTrace: " + ex.StackTrace);
            }
        }
        public Event (EventType e, double t, StationClass s, User u = null)
        {
            type = e;
            time = Math.Round(t,2);
            station = s;
            date = DateTime.Now;
            user = u;
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"type",type.ToString().Quotate() },
                {"date",date.ToString("O").Quotate() },
                {"time",time.ToString() },
                {"station",station.Name.Quotate() },
                {"userID",(user != null ? user.UID.Quotate(): "".Quotate())}
            };
            return obj.Stringify();
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
        public EventType type;
        public double time;
        public StationClass station;
        public DateTime date;
        public User user;
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
    
    abstract internal class Traveler
    {
        #region Public Methods
        public Traveler() { }
        // Gets the base properties and orders of the traveler from a json string
        public Traveler(string json)
        {
            Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
            m_ID = Convert.ToInt32(obj["ID"]);
            
            m_quantity = Convert.ToInt32(obj["quantity"]);
            m_part = new Bill(obj["itemCode"], 1, m_quantity);
            Items = new List<TravelerItem>();
            foreach (string item in (new StringStream(obj["items"])).ParseJSONarray())
            {
                Items.Add(new TravelerItem(item));
            }
            m_parentOrders = (new StringStream(obj["parentOrders"])).ParseJSONarray();
            m_station = StationClass.GetStation(obj["station"]);
            m_state = (ItemState)Enum.Parse(typeof(ItemState), obj["state"]);
            m_dateStarted = obj["dateStarted"];
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
            m_part = new Bill(billNo,1,quantity);
            m_quantity = quantity;
            m_parentOrders = new List<string>();
            m_station = StationClass.GetStation("Start");
            Items = new List<TravelerItem>();
            NewID();
            m_state = ItemState.PreProcess;
            m_dateStarted = "";
        }
        public virtual void ImportPart(IOrderManager orderManager, ref OdbcConnection MAS)
        {
            m_part = new Bill(m_part.BillNo,1, m_quantity, ref MAS);
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
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"ID",m_ID.ToString() },
                {"itemCode",m_part.BillNo.Quotate() },
                {"quantity",m_quantity.ToString() },
                {"items",Items.Stringify<TravelerItem>() },
                {"parentOrders",m_parentOrders.Stringify<string>() },
                {"station",m_station.Name.Quotate() },
                {"state",m_state.ToString().Quotate() },
                {"type",this.GetType().ToString().Quotate()},
                {"dateStarted",DateStarted.Quotate() }
            };
            return obj.Stringify();
        }
        // print a label for this traveler
        public string PrintLabel(ushort itemID, LabelType type, int qty = 1, bool forcePrint = false)
        {
            string result = "";
            try
            {
                
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
                    Dictionary < string, string> labelConfigs = (new StringStream(ConfigManager.Get("print"))).ParseJSON();
                    // only print if the config says so
                    if (forcePrint || (labelConfigs.ContainsKey(type.ToString()) && Convert.ToBoolean(labelConfigs[type.ToString()])))
                    {
                        result = client.UploadString(new StringStream(ConfigManager.Get("labelServer")).ParseJSON()["address"], "POST", json);
                    } else
                    {
                        result = "Labels disabled";
                    }
                }
            }
            catch (Exception ex)
            {
                result = "Error when printing label";
                Console.WriteLine(result + " " + ex.Message); 
            }
            return result;
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
            TravelerItem item = FindItem(ID);
            item.Scrapped = true;
            item.State = ItemState.PostProcess;
            item.Station = StationClass.GetStation("Scrapped");
        }
        public void FinishItem(ushort ID)
        {
            TravelerItem item = FindItem(ID);
            // now in post process
            item.State = ItemState.PostProcess;
            item.History.Add(new Event(EventType.Finished, 0.0, item.Station));
            // check to see if this concludes the traveler
            if (Items.Where(x => x.State == ItemState.PostProcess && !x.Scrapped).Count() >= m_quantity && Items.All(x => x.State == ItemState.PostProcess))
            {
                State = ItemState.PostProcess;
            }
            // add this item to inventory
            InventoryManager.Add(ItemCode);
        }
        public void EnterProduction()
        {
            m_state = ItemState.InProcess;
            m_dateStarted = DateTime.Today.ToString("MM/dd/yyyy");
        }
        // advances all completed items at the specified station
        public void Advance(StationClass station)
        {
            foreach (TravelerItem item in Items)
            {
                if (item.Station == station && item.IsComplete())
                {
                    AdvanceItem(item.ID);
                }
            }
        }

        public TravelerItem AddItem(StationClass station)
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
        public int QuantityPendingAt(StationClass station)
        {
            int quantityPending = 0;
            if (station != null)
            {
                quantityPending += Items.Where(x => x.Station == station && !x.History.Exists(e => e.station == station && e.type == EventType.Completed)).Count();
                // these stations can create items
                if (station.Creates.Count > 0 && m_station == station)
                {
                    quantityPending = m_quantity - Items.Where(x => !x.Scrapped).Count(); // calculates the total item deficit for this traveler
                }
            }
            return quantityPending;
        }
        public int QuantityAt(StationClass station)
        {
            if (station != null)
            {
                return Items.Where(x => x.Station == station).Count();
            }
            return 0;
        }
        public int QuantityScrapped()
        {
            return Items.Where(x => x.Scrapped).Count();
        }
        public int QuantityCompleteAt(StationClass station)
        {
            return Items.Where(x => x.Station == station && x.History.Exists(e => e.station == station && e.type == EventType.Completed)).Count();
        }
        // export for clients to display
        public string Export(string clientType, StationClass station)
        {
            string json = "";
            json += "{";
            json += "\"type\":" + this.GetType().Name.Quotate() + ',';
            json += "\"ID\":" + m_ID + ",";
            json += "\"itemCode\":" + '"' + m_part.BillNo + '"' + ",";
            json += "\"quantity\":" + m_quantity + ",";
            json += "\"items\":" + Items.Stringify() + ',';
            json += "\"state\":" + m_state.ToString().Quotate() + ',';
            if (clientType == "OperatorClient")
            {
                json += "\"laborRate\":" + GetCurrentLabor() + ",";
                json += "\"station\":" + station.Name.Quotate() + ",";
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
                List<string> stations = new List<string>();
                if (m_station == StationClass.GetStation("Start") || QuantityPendingAt(m_station) > 0 || QuantityAt(m_station) > 0) stations.Add(m_station.Name);
                foreach (TravelerItem item in Items)
                {
                    if (!stations.Exists(x => item.Station.Is(x)))
                    {
                        stations.Add(item.Station.Name);
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
                {"Date started", m_dateStarted.Quotate() },
                {"ID",m_ID.ToString() },
                {"Model",m_part.BillNo.Quotate() },
                {"Description",m_part.BillDesc.Quotate() },
                {"Qty on traveler",m_quantity.ToString() },
                {"Orders",m_parentOrders.Stringify() },
                {"Items",Items.Stringify() },
                {"Starting station",m_station.Name.Quotate() }
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
        // export for csv header
        public static string ExportCSVheader()
        {
            List<string> header = new List<string>();
            header.Add("Traveler");
            header.Add("Part");
            header.Add("Description");
            header.Add("Quantity");
            header.Add("Station");
            return header.Stringify<string>(false).Trim('[').Trim(']');
        }
        // export for csv detail
        public virtual string ExportCSVdetail()
        {
            List<string> detail = new List<string>();
            detail.Add(m_ID.ToString());
            detail.Add(m_part.BillNo.Quotate());
            detail.Add(m_part.BillDesc.Quotate());
            detail.Add(m_quantity.ToString());
            detail.Add(m_station.Name.Quotate());
            return detail.Stringify<string>(false).Trim('[').Trim(']');
        }
        #endregion
        //--------------------------------------------------------
        #region Abstract Methods

        public abstract string ExportTableRows(string clientType, StationClass station);
        // advances the item to the next station
        public abstract void AdvanceItem(ushort ID);
        // gets the next station for the given item
        public abstract StationClass GetNextStation(UInt16 itemID);
        // gets the work rate for the current station
        public abstract double GetCurrentLabor();
        // gets the total work wrapped up in the given station
        public abstract double GetTotalLabor(StationClass station);
        // overridden in derived classes, packs properties into the Export() json string
        protected abstract string ExportProperties();
        #endregion
        //--------------------------------------------------------
        #region Private Methods
        protected double GetRate(Bill bill, StationClass station,bool total = false)
        {
            try
            {
                foreach (Item componentItem in bill.ComponentItems)
                {
                    if (station.LaborCodes.Exists(laborCode => laborCode == componentItem.ItemCode))
                    {
                        return (total ? componentItem.TotalQuantity : componentItem.QuantityPerBill);
                    }
                }
            } catch (Exception ex)
            {
                Server.LogException(ex);
            }
            return 0.0;
        }
        #endregion
        //--------------------------------------------------------
        #region Properties

        // general
        protected int m_ID;
        protected Bill m_part;
        protected int m_quantity;
        private List<TravelerItem> items;
        protected List<string> m_parentOrders;
        private StationClass m_station;
        private ItemState m_state;
        private string m_dateStarted;
        private int m_priority;

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
            internal StationClass Station
            {
                get
                {
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

            internal ItemState State
            {
                get
                {
                    return m_state;
                }
                set
                {
                    m_state = value;
                }
            }

        public string DateStarted
        {
            get
            {
                return m_dateStarted;
            }

            set
            {
                m_dateStarted = value;
            }
        }

        public int Priority
        {
            get
            {
                return m_priority;
            }

            set
            {
                m_priority = value;
            }
        }
        #endregion
    }
}
