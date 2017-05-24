
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
    enum LabelType
    {
        Tracking,
        Scrap,
        Pack,
        Table,
        Test
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
            try
            {
                json += '{';
                json += "\"name\":" + '"' + Name.Replace("\"", "\\\"") + '"' + ',';
                json += "\"value\":" + '"' + (Value != null ? Value.ToString().Replace("\"", "\\\"") : "") + '"' + ',';
                json += "\"qty\":" + '"' + Qty.ToString().Replace("\"", "\\\"") + '"';
                json += '}';
            } catch (Exception ex)
            {
                Server.LogException(ex);
            }
            return json;
        }
        public string Name;
        public valueType Value;
        public qtyType Qty;
    }
    
    abstract internal class Traveler : IForm
    {
        #region Public Methods
        public Traveler() {
            NewID();
            m_quantity = 0;
            items = new List<TravelerItem>();
            m_parentOrderNums = new List<string>();
            m_parentOrders = new List<Order>();
            m_parentIDs = new List<int>();
            m_parentTravelers = new List<Traveler>();
            m_childIDs = new List<int>();
            m_childTravelers = new List<Traveler>();
            m_station = null;
            m_state = 0;
            m_dateStarted = "";
            m_comment = "";
        }
        public Traveler(Form form) : this()
        {
            Update(form);
        }
        // Gets the base properties and orders of the traveler from a json string
        public Traveler(string json) : this()
        {
            Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
            m_ID = Convert.ToInt32(obj["ID"]);
            
            m_quantity = Convert.ToInt32(obj["quantity"]);
            
            foreach (string item in (new StringStream(obj["items"])).ParseJSONarray())
            {
                Items.Add(new TravelerItem(item));
            }
            m_parentOrderNums = (new StringStream(obj["parentOrders"])).ParseJSONarray();
            foreach (string id in (new StringStream(obj["parentTravelers"])).ParseJSONarray())
            {
                m_parentIDs.Add(Convert.ToInt32(id));
            }
           
            foreach (string id in (new StringStream(obj["childTravelers"])).ParseJSONarray())
            {
                m_childIDs.Add(Convert.ToInt32(id));
            }
            
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
        public Traveler(int quantity) : this()
        {
            // set META information
           
            m_quantity = quantity;
            m_station = StationClass.GetStation("Start");
            NewID();
            m_state = ItemState.PreProcess;
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
                
                {"quantity",m_quantity.ToString() },
                {"items",Items.Stringify<TravelerItem>() },
                {"parentOrders",m_parentOrderNums.Stringify<string>() },
                {"parentTravelers",m_parentTravelers.Select( x => x.ID).ToList().Stringify<int>() }, // stringifies a list of IDs
                {"childTravelers",m_childTravelers.Select( x => x.ID).ToList().Stringify<int>() }, // stringifies a list of IDs
                {"station",m_station.Name.Quotate() },
                {"state",m_state.ToString().Quotate() },
                {"type",this.GetType().Name.Quotate()},
                {"dateStarted",DateStarted.Quotate() }
            };
            return obj.Stringify();
        }
        // print a label for this traveler
        public virtual string PrintLabel(ushort itemID, LabelType type, int qty = 1, bool forcePrint = false)
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
                        case LabelType.Tracking:    template = "4x2 Table Travel1";     printer = "4x2Heian2"; break; // 4x2Pack --> in hall
                        case LabelType.Scrap:       template = "4x2 Table Scrap1";      printer = "4x2Heian2"; break;
                        case LabelType.Pack:        template = "4x2 Table Carton EATS"; printer = "4x2FloorTableBox"; break;
                        case LabelType.Table:       template = "4x6 Table EATS";        printer = "4x6FloorTable"; break;
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
                Server.LogException(ex);
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
        
        public virtual void EnterProduction(ITravelerManager travelerManager)
        {
            m_state = ItemState.InProcess;
            m_dateStarted = DateTime.Today.ToString("MM/dd/yyyy");
        }
        // advances all completed items at the specified station
        public void Advance(StationClass station, ITravelerManager travelerManager = null)
        {
            foreach (TravelerItem item in Items)
            {
                if (item.Station == station && item.IsComplete())
                {
                    AdvanceItem(item.ID, travelerManager);
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
                quantityPending += Items.Where(x => x.Station == station && !x.History.OfType<ProcessEvent>().ToList().Exists(e => e.Station == station && e.Process == ProcessType.Completed)).Count();
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
        public int QuantityScrappedAt(StationClass station)
        {
            return Items.Where(x => x.Station == station && x.History.OfType<ProcessEvent>().ToList().Exists(e => e.Station == station && e.Process == ProcessType.Scrapped)).Count();
        }
        public int QuantityCompleteAt(StationClass station)
        {
            return Items.Where(x => x.Station == station && x.History.OfType<ProcessEvent>().ToList().Exists(e => e.Station == station && e.Process == ProcessType.Completed)).Count();
        }
        
        public string ExportStationSummary(StationClass station)
        {
            Dictionary<string, string> detail = new Dictionary<string, string>();
            if (station == StationClass.GetStation("Start"))
            {
                detail.Add("qtyPending", m_quantity.ToString());
            } else
            {
                detail.Add("qtyPending", QuantityPendingAt(station).ToString());
            }
            
            detail.Add("qtyCompleted", QuantityCompleteAt(station).ToString());
            detail.Add("qtyScrapped", QuantityScrappedAt(station).ToString());
            return detail.Stringify();
        }
        public List<StationClass> CurrentStations()
        {
            List<StationClass> stations = new List<StationClass>();
            foreach (StationClass station in StationClass.GetStations())
            {
                if (QuantityPendingAt(station) > 0 || QuantityAt(station) > 0) stations.Add(station);
            }
            if (Station == StationClass.GetStation("Start")) stations.Add(Station);
            return stations;
        }
        // export for clients to display
        public virtual string Export(string clientType, StationClass station)
        {
            Dictionary<string, string> obj = new StringStream(ToString()).ParseJSON(false);
            obj["type"] = obj["type"].Replace(System.Reflection.Assembly.GetExecutingAssembly().GetName().Name.Replace(' ','_') + ".", "");

            obj = obj.Concat(ExportProperties(station)).ToDictionary(x => x.Key, x => x.Value);
            if (station != null) {
                obj["station"] = station.Name.Quotate();
                obj.Add("qtyPending", QuantityPendingAt(station).ToString());
                obj.Add("qtyScrapped", QuantityScrapped().ToString());
                obj.Add("qtyCompleted", QuantityCompleteAt(station).ToString());
                obj.Add("totalLabor",Math.Round(GetTotalLabor()).ToString());
                obj.Add("members", '[' + ExportTableRows(clientType, station) + ']');
            }

            if (clientType == "SupervisorClient") {
                List<string> stations = new List<string>();
                if (m_station == StationClass.GetStation("Start") || QuantityPendingAt(m_station) > 0 || QuantityAt(m_station) > 0) stations.Add(m_station.Name);
                foreach (TravelerItem item in Items)
                {
                    if (!stations.Exists(x => item.Station.Is(x)))
                    {
                        stations.Add(item.Station.Name);
                    }
                }
                obj.Add("stations", stations.Stringify<string>());
            }
            return obj.Stringify();
            //string json = "";
            //json += "{";
            //json += "\"type\":" + this.GetType().Name.Quotate() + ',';
            //json += "\"ID\":" + m_ID + ",";
            //json += "\"quantity\":" + m_quantity + ",";
            //json += "\"items\":" + Items.Stringify() + ',';
            //json += "\"state\":" + m_state.ToString().Quotate() + ',';
            //if (clientType == "OperatorClient")
            //{
            //    json += "\"laborRate\":" + GetCurrentLabor() + ",";
            //    json += "\"station\":" + station.Name.Quotate() + ",";
            //    json += "\"qtyPending\":" + QuantityPendingAt(station) + ",";
            //    json += "\"qtyScrapped\":" + QuantityScrapped() + ",";
            //    json += "\"qtyCompleted\":" + QuantityCompleteAt(station) + ",";
            //    json += "\"members\":[";

            //    json += ExportTableRows(clientType, station);

            //    json += "]";
            //}
            //else if (clientType == "SupervisorClient")
            //{
            //    json += "\"stations\":";
            //    List<string> stations = new List<string>();
            //    if (m_station == StationClass.GetStation("Start") || QuantityPendingAt(m_station) > 0 || QuantityAt(m_station) > 0) stations.Add(m_station.Name);
            //    foreach (TravelerItem item in Items)
            //    {
            //        if (!stations.Exists(x => item.Station.Is(x)))
            //        {
            //            stations.Add(item.Station.Name);
            //        }
            //    }
            //    json += stations.Stringify();
            //}
            //json += "}";
            //return json;

        }
        // export for JSON viewer
        public virtual string ExportHuman()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"Date started", m_dateStarted.Quotate() },
                {"ID",m_ID.ToString() },
                {"Qty on traveler",m_quantity.ToString() },
                {"Orders",m_parentOrderNums.Stringify() },
                {"Items",Items.Stringify() },
                {"Starting station",m_station.Name.Quotate() }
            };
            return obj.Stringify();
        }
        // export for summary view
        public virtual string ExportSummary()
        {
            int qtyPending = m_quantity - Items.Where(x => !x.Scrapped).Count();
            int qtyComplete = QuantityAt(StationClass.GetStation("Finished"));
            int qtyInProcess = Items.Where(x => !x.Scrapped).Count() - qtyComplete;
            // Displays properties in order
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"Traveler",m_ID.ToString() },
                {"Pending",qtyPending.ToString()},
                {"In process",qtyInProcess.ToString()},
                {"Scrapped",QuantityScrapped().ToString() },
                {"Complete",qtyComplete.ToString() },
                {"Orders",m_parentOrderNums.Stringify() }
            };
            return obj.Stringify();
        }
        // export for csv header
        public static string ExportCSVheader()
        {
            List<string> header = new List<string>();
            header.Add("Traveler");
            header.Add("Quantity");
            header.Add("Soonest Ship");
            header.Add("Station");
            return header.Stringify<string>().Trim('[').Trim(']');
        }
        // export for csv detail
        public virtual string ExportCSVdetail()
        {
            List<string> detail = new List<string>();
            detail.Add(m_ID.ToString());
            detail.Add(m_quantity.ToString());
            detail.Add(SoonestShipDate.ToString("MM/dd/yyyy"));
            detail.Add(m_station.Name);
            return detail.Stringify<string>().Trim('[').Trim(']');
        }

        public virtual void Update(Form form)
        {
            Quantity = Convert.ToInt32(form.ValueOf("quantity"));
            Comment = form.ValueOf("comment");
            Station = StationClass.GetStation(form.ValueOf("station"));
        }
        public virtual Form CreateForm()
        {
            Form form = new Form(this.GetType());
            form.Integer("quantity", "Quantity");
            form.Textbox("comment", "Comment");
            form.Selection("station", "Starting Station", StationClass.StationNames());
            return form;
        }
        public virtual Form CreateFilledForm()
        {
            Form form = new Form(this.GetType());
            form.Integer("quantity", "Quantity",m_quantity);
            form.Textbox("comment", "Comment",m_comment);
            form.Selection("station", "Starting Station",new List<string>(), m_station.Name);
            return form;
        }
        #endregion
        //--------------------------------------------------------
        #region Abstract Methods
        // finishes an item
        public virtual void FinishItem(ushort ID)
        {
            TravelerItem item = FindItem(ID);
            // now in post process
            item.State = ItemState.PostProcess;
            item.History.Add(new LogEvent(null, LogType.Finish, item.Station));
            // check to see if this concludes the traveler
            if (Items.Where(x => x.State == ItemState.PostProcess && !x.Scrapped).Count() >= m_quantity && Items.All(x => x.State == ItemState.PostProcess))
            {
                State = ItemState.PostProcess;
            }
        }
        // returns true if the specified traveler can combine with this one
        public abstract bool CombinesWith(object[] args);
        public abstract string ExportTableRows(string clientType, StationClass station);
        // advances the item to the next station
        public abstract void AdvanceItem(ushort ID, ITravelerManager travelerManager = null);
        // gets the next station for the given item
        public abstract StationClass GetNextStation(UInt16 itemID);
        // gets the work rate for the current station
        public abstract double GetCurrentLabor(StationClass station = null);
        // gets the total work wrapped up in the given station
        public abstract double GetTotalLabor(StationClass station = null);
        // overridden in derived classes, packs properties into the Export() json string
        public abstract Dictionary<string, string> ExportProperties(StationClass station = null);
        // pre
        public abstract void ImportInfo(ITravelerManager travelerManager, IOrderManager orderManager, ref OdbcConnection MAS);
        #endregion
        //--------------------------------------------------------
        #region Private Methods
        protected double GetRate(Bill bill, StationClass station,bool total = false)
        {
            try
            {
                foreach (Item componentItem in bill.ComponentItems)
                {
                    double rate = GetRate(componentItem, station, total);
                    if (rate > 0.0) return rate;
                }
            } catch (Exception ex)
            {
                Server.LogException(ex);
            }
            return 0.0;
        }
        protected double GetRate(Item item, StationClass station, bool total = false)
        {
            if (station != null && item != null && station.LaborCodes.Exists(laborCode => laborCode == item.ItemCode))
            {
                return (total ? item.TotalQuantity : item.QuantityPerBill);
            }
            return 0.0;
        }
        #endregion
        //--------------------------------------------------------
        #region Properties

        // general
        protected int m_ID;
        
        protected int m_quantity;
        private List<TravelerItem> items;
        private string m_comment;
        // linking ^^^^^^^^^^^^^^^^^^^^^
        private List<string> m_parentOrderNums;
        private List<Order> m_parentOrders;

        private List<int> m_parentIDs;
        private List<Traveler> m_parentTravelers;

        private List<int> m_childIDs;
        private List<Traveler> m_childTravelers;
        //^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
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

        internal List<string> ParentOrderNums
        {
            get
            {
                return m_parentOrderNums;
            }

            set
            {
                m_parentOrderNums = value;
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

        internal string DateStarted
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

        internal int Priority
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

        internal List<Traveler> ParentTravelers
        {
            get
            {
                return m_parentTravelers;
            }

            set
            {
                m_parentTravelers = value;
            }
        }

        internal List<Traveler> ChildTravelers
        {
            get
            {
                return m_childTravelers;
            }

            set
            {
                m_childTravelers = value;
            }
        }

        public List<int> ParentIDs
        {
            get
            {
                return m_parentIDs;
            }

            set
            {
                m_parentIDs = value;
            }
        }

        public List<int> ChildIDs
        {
            get
            {
                return m_childIDs;
            }

            set
            {
                m_childIDs = value;
            }
        }

        internal List<Order> ParentOrders
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
        internal DateTime SoonestShipDate
        {
            get
            {
                return ParentOrders.Max(y => y.ShipDate);
            }
        }

        public string Comment
        {
            get
            {
                return m_comment;
            }

            set
            {
                m_comment = value;
            }
        }
        #endregion
    }
}
