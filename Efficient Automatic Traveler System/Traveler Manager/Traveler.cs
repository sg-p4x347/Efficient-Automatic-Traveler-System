
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data.Odbc;
using System.Net;
using System.Net.Http;
using System.Data;

namespace Efficient_Automatic_Traveler_System
{
    public enum LabelType
    {
        Tracking,
        Scrap,
        Pack,
        Table,
        Chair,
        ChairCarton,
        MixedCarton,
        Box
    }
    
    
    public struct NameValueQty<valueType,qtyType>
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
    
    abstract public class Traveler : IForm
    {
        #region Public Methods
        public Traveler() {
            NewID();
            m_itemCode = "";
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
            m_lastReworkAccountedFor = 0;
        }
        public Traveler(Form form) : this()
        {
            ItemCode = form.ValueOf("itemCode");
            Update(form);
        }
        // Gets the base properties and orders of the traveler from a json string
        public Traveler(string json, Version version) : this()
        {
            Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
            m_ID = Convert.ToInt32(obj["ID"]);
            m_itemCode = obj["itemCode"];
            m_quantity = Convert.ToInt32(obj["quantity"]);
            
            foreach (string item in (new StringStream(obj["items"])).ParseJSONarray())
            {
                TravelerItem itemObj = new TravelerItem(item);
                itemObj.Parent = this;
                Items.Add(itemObj);
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
            if (obj["state"] == "PostProcess") obj["state"] = "Finished";// old DB conversion
            m_state = (GlobalItemState)Enum.Parse(typeof(GlobalItemState), obj["state"]);
            m_dateStarted = obj["dateStarted"];
            m_comment = obj["comment"];
            m_lastReworkAccountedFor = (ushort)(obj.ContainsKey("lastReworkAccountedFor") ? Convert.ToUInt16(obj["lastReworkAccountedFor"]) : 0);
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
        public Traveler(string itemCode, int quantity) : this()
        {
            m_itemCode = itemCode;
            m_quantity = quantity;
            m_station = StationClass.GetStation("Start");
            State = GlobalItemState.PreProcess;
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
                {"itemCode",m_itemCode.Quotate() },
                {"quantity",m_quantity.ToString() },
                {"items",Items.Stringify<TravelerItem>() },
                {"parentOrders",m_parentOrders.Select(o => o.SalesOrderNo).ToList<string>().Stringify<string>() },
                {"parentTravelers",m_parentTravelers.Select( x => x.ID).ToList().Stringify<int>() }, // stringifies a list of IDs
                {"childTravelers",m_childTravelers.Select( x => x.ID).ToList().Stringify<int>() }, // stringifies a list of IDs
                {"station",m_station.Name.Quotate() },
                {"state",GetGlobalState().ToString().Quotate() },
                {"type",this.GetType().Name.Quotate()},
                {"dateStarted",DateStarted.Quotate() },
                {"comment",Comment.Quotate() },
                {"lastReworkAccountedFor",m_lastReworkAccountedFor.ToString() }
            };
            return obj.Stringify();
        }
        // print a label for this traveler
        public virtual string PrintLabel(ushort itemID, LabelType type, int? qty = null, bool forcePrint = false, StationClass station = null)
        {
            string result = "";
            try
            {
                TravelerItem item = FindItem(itemID);
                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.ContentType] = "application/json";
                    string json = "{";
                    string fields = GetLabelFields(itemID, type);
                    string printer = "";
                    string template = "";
                    // TEMP
                    //type = LabelType.Test;
                    string size = "";
                    switch (type)
                    {
                        case LabelType.Tracking:    template = "4x2 Table Travel1";             break; // 4x2Pack --> in hall
                        case LabelType.Scrap:       template = "4x2 Table Scrap1";              break;
                        case LabelType.Pack:        template = "4x2 Table Carton EATS";
                            if (qty == null)
                            {
                                qty = 2;
                            }
                            break;
                        case LabelType.Table:       template = "4x6 Table EATS";                break;
                        case LabelType.Chair:       template = "4x2 EdChair EATS";              break;
                        case LabelType.ChairCarton: template = "4x6 EdChair Pack Carton EATS";  break;
                        case LabelType.Box:         template = "4x2 Table Travel Box";          break;
                    }
                    if (qty == null) qty = 1;
                    size = template.Substring(0, 3).ToLower();
                    if (station == null) station = item.Station;
                    printer = station.Printers.Find(x => x.ToLower().Contains(size));
                    if (printer == "")
                    {
                        throw new Exception("Could not find a " + size + " printer for this station when printing a [" + template + "] , check the config.json file for a printer listing on this station");
                    }
                    if (Convert.ToBoolean(ConfigManager.Get("debug")))
                    {
                        printer = "4x2IT";
                    }
                    //switch (type)
                    //{
                    //    case LabelType.Tracking: template = "4x2 Table Travel1"; printer = "4x2Heian2"; break; // 4x2Pack --> in hall
                    //    case LabelType.Scrap: template = "4x2 Table Scrap1"; printer = "4x2Heian2"; break;
                    //    case LabelType.Pack: template = "4x2 Table Carton EATS"; printer = "4x2FloorTableBox"; break;
                    //    case LabelType.Table: template = "4x6 Table EATS"; printer = "4x6FloorTable"; break;
                    //    case LabelType.Test: template = "4x2 Table Carton EATS logo"; printer = "4x2IT"; break;
                    //}
                    // piecing it together

                    if (fields.Length > 0) { json += fields.Trim(',') + ','; }
                    json += "\"printer\":\"" + printer + "\"";
                    json += ",\"template\":\"" + template + "\"";
                    json += ",\"qty\":" + qty.Value;
                    json += '}';
                    Dictionary < string, string> labelConfigs = (new StringStream(ConfigManager.Get("print"))).ParseJSON();
                    // only print if the config says so
                    if (forcePrint || (labelConfigs.ContainsKey(type.ToString()) && Convert.ToBoolean(labelConfigs[type.ToString()])))
                    {
                        result = client.UploadString(new StringStream(ConfigManager.Get("labelServer")).ParseJSON()["address"], "POST", json);
                        result += " at " + printer + " printer";
                    } else
                    {
                        result = type.ToString() + " Labels disabled";
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
        public GlobalItemState GetGlobalState()
        {
            if (State != GlobalItemState.PreProcess)
            {
                // determine if this is finished
                if (Items.Count(i => i.Finished) >= Quantity)
                {
                    return GlobalItemState.Finished;
                }
                else
                {
                    return GlobalItemState.InProcess;
                }
            }
            return GlobalItemState.PreProcess;
        }
        // print a traveler pack label
        public abstract string GetLabelFields(ushort itemID, LabelType type);

        public static bool IsTable(string s)
        {
            return s != null && ((s.Length <= 12 && s.Substring(0, 2) == "MG") || (s.Length <= 12 && (s.Substring(0, 3) == "38-" || s.Substring(0, 3) == "41-")));
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
        public bool FindItem(ushort ID, out TravelerItem item)
        {
            item = Items.Find(x => x.ID == ID);
            return item != null;
        }
        public string PrintSequenceID(TravelerItem item)
        {
            string sequenceID = ID.ToString("D6");
            if (item != null) {
                if (item.Scrapped)
                {
                    sequenceID += "-Scrap #" + ScrapSequenceNo(item);
                }
                else
                {
                    sequenceID += "-" + (item.Replacement ? "R" : "") + item.SequenceNo.ToString() + '/' + Quantity.ToString();
                }
            }
            return sequenceID;
        }
        public string PrintID(TravelerItem item = null)
        {
            string id = ID.ToString();
            if (item != null)
            {
                id += "-" + item.ID.ToString();
            }
            return id;
        }
        //public void ScrapItem(ushort ID)
        //{
        //    TravelerItem item = FindItem(ID);
        //    //item.SequenceNo = (ushort)(QuantityScrapped() + 1);
        //    item.Scrapped = true;
        //    item.State = ItemState.PostProcess;
            
        //}
        
        public virtual void EnterProduction(ITravelerManager travelerManager)
        {
            m_state = GlobalItemState.InProcess;
            m_dateStarted = DateTime.Today.ToString("MM/dd/yyyy");
        }
        // advances all completed items at the specified station
        //public virtual void Advance(StationClass station, ITravelerManager travelerManager = null)
        //{
        //    foreach (TravelerItem item in CompletedItems(station))
        //    {
        //        AdvanceItem(item.ID, travelerManager);
        //    }
        //}
        public TravelerItem AddItem(StationClass station)
        {
            // find the highest id
            // and find the smallest available sequence number
            ushort highestID = Items.Count > 0 ? Items.Max(i => i.ID) : (ushort)0;
            int maxSeqNo = (Items.Count > 0 ? Items.Max(x => x.SequenceNo) : 0);
            bool[] sequenceSlots = new bool[maxSeqNo+1];
            foreach (TravelerItem item in Items)
            {
                if (!item.Scrapped) sequenceSlots[item.SequenceNo] = true;
            }
            ushort sequenceNo = (ushort)(maxSeqNo + 1);
            bool replacement = false;
            for (ushort i = 1; i < sequenceSlots.Length; i++)
            {
                if (!sequenceSlots[i])
                {
                    // hole in sequence was found, this is a replacement
                    sequenceNo = i;
                    replacement = true;
                    break;
                }
            }
            // use the next id (highest + 1)
            TravelerItem newItem = new TravelerItem(ItemCode,(ushort)(highestID + 1), sequenceNo,station,replacement);
            newItem.Parent = this;
            Items.Add(newItem);


            // print le label
            LabelType labelType = LabelType.Tracking;
            if (this is Chair)
            {
                labelType = LabelType.Chair;
            }
            else if (this is Box)
            {
                labelType = LabelType.Box;
            }
            PrintLabel(newItem.ID, labelType);
            //if (station.CreatesThis(this) && this is Table)
            //{
            //    int boxQuantity = Items.Count(i => !i.Scrapped) - ChildTravelers.OfType<TableBox>().Sum(child => child.Quantity);
            //    if (boxQuantity > 0)
            //    {
            //        // Create a box traveler for these items
            //        TableBox box = (this as Table).CreateBoxTraveler();
            //        box.Quantity = boxQuantity;
            //        box.EnterProduction(Server.TravelerManager);
            //        Server.TravelerManager.GetTravelers.Add(box);
            //    }
            //}
            return newItem;
        }
        public void Finish()
        {
            State = GlobalItemState.Finished;
            Server.TravelerManager.OnTravelersChanged(this);
        }
        public int QuantityPendingAt(StationClass station)
        {

            int quantityPending = Items.Count(i => i.PendingAt(station));
            //these stations can create items
            if (station.Creates.Count > 0 && m_station == station)
            {
                quantityPending = m_quantity - Items.Where(x => !x.Scrapped).Count(); // calculates the total item deficit for this traveler
            }
            return quantityPending;
            //if (station != null)
            //{
            //    quantityPending += Items.Where(x => x.Station == station && !x.History.OfType<ProcessEvent>().ToList().Exists(e => e.Station == station && e.Process == ProcessType.Completed)).Count();
            //    // these stations can create items
            //    if (station.Creates.Count > 0 && m_station == station)
            //    {
            //        quantityPending = m_quantity - Items.Where(x => !x.Scrapped).Count(); // calculates the total item deficit for this traveler
            //    }
            //}
            //return quantityPending;
        }
        public int QuantityInProcessAt(StationClass station)
        {
            return Items.Count(i => i.Station == station && i.LocalState == LocalItemState.InProcess);
        }
        public int QuantityPostProcessAt(StationClass station)
        {
            return Items.Count(i => i.Station == station && i.LocalState == LocalItemState.PostProcess);
        }
        public int QuantityAt(StationClass station)
        {
            if (station != null)
            {
                return Items.Count(x => x.Station == station);
            }
            return 0;
        }
        public int QuantityScrapped()
        {
            return items.Count(i => i.Scrapped);
        }
        public int QuantityScrappedAt(StationClass station)
        {
            return Items.Where(x => x.Station == station && x.History.OfType<ProcessEvent>().ToList().Exists(e => e.Station == station && e.Process == ProcessType.Scrapped)).Count();
        }
        public int QuantityCompletedAt(StationClass station, DateTime date)
        {
            return Items.Count(i => i.BeenCompletedDuring(date));
        }
        public int QuantityOrdered()
        {
            return ParentOrders.Sum(o => o.FindItems(ID).Sum(i => i.QtyOrdered));
        }
        //public string ExportStationSummary(StationClass station)
        //{
        //    Dictionary<string, string> detail = new Dictionary<string, string>();
        //    if (station == StationClass.GetStation("Start"))
        //    {
        //        detail.Add("qtyPending", m_quantity.ToString());
        //    }
        //    else if (station == StationClass.GetStation("Finished"))
        //    {
        //        detail.Add("qtyPending", QuantityAt(station).ToString());
        //    }
        //    else
        //    {
        //        detail.Add("qtyPending", QuantityPendingAt(station).ToString());
        //    }
            
            
        //    detail.Add("qtyCompleted", QuantityCompleteAt(station).ToString());
        //    detail.Add("qtyScrapped", QuantityScrappedAt(station).ToString());
        //    return detail.Stringify();
        //}
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
        public List<StationClass> CurrentStations(GlobalItemState viewState)
        {
            List<StationClass> stations = new List<StationClass>();
            foreach (StationClass station in StationClass.GetStations())
            {
                if (Items.Exists(i => i.GlobalState == viewState && i.Station == station)) stations.Add(station);
            }
            if ((viewState == GlobalItemState.PreProcess && State == GlobalItemState.PreProcess) 
                || (viewState == GlobalItemState.InProcess && State == GlobalItemState.InProcess)) stations.Add(Station);
            return stations;
        }
        
        // export for clients to display
        //public virtual string Export(string clientType, StationClass station)
        //{
        //    Dictionary<string, string> obj = new StringStream(ToString()).ParseJSON(false);
        //    obj["type"] = obj["type"].Replace(System.Reflection.Assembly.GetExecutingAssembly().GetName().Name.Replace(' ','_') + ".", "");

        //    obj = obj.Concat(ExportProperties(station)).ToDictionary(x => x.Key, x => x.Value);
        //    if (station != null) {
        //        obj["station"] = station.Name.Quotate();
        //        obj.Add("qtyPending", QuantityPendingAt(station).ToString());
        //        obj.Add("qtyScrapped", QuantityScrapped().ToString());
        //        obj.Add("qtyCompleted", QuantityCompleteAt(station).ToString());
        //        obj.Add("totalLabor",Math.Round(GetTotalLabor()).ToString());
        //        obj.Add("members", '[' + ExportTableRows( station) + ']');
        //    }

        //    if (clientType == "SupervisorClient") {
        //        List<string> stations = new List<string>();
        //        if (m_station == StationClass.GetStation("Start") || QuantityPendingAt(m_station) > 0 || QuantityAt(m_station) > 0) stations.Add(m_station.Name);
        //        foreach (TravelerItem item in Items)
        //        {
        //            if (!stations.Exists(x => item.Station.Is(x)))
        //            {
        //                stations.Add(item.Station.Name);
        //            }
        //        }
        //        obj.Add("stations", stations.Stringify<string>());
        //    }
        //    return obj.Stringify();
        //    //string json = "";
        //    //json += "{";
        //    //json += "\"type\":" + this.GetType().Name.Quotate() + ',';
        //    //json += "\"ID\":" + m_ID + ",";
        //    //json += "\"quantity\":" + m_quantity + ",";
        //    //json += "\"items\":" + Items.Stringify() + ',';
        //    //json += "\"state\":" + m_state.ToString().Quotate() + ',';
        //    //if (clientType == "OperatorClient")
        //    //{
        //    //    json += "\"laborRate\":" + GetCurrentLabor() + ",";
        //    //    json += "\"station\":" + station.Name.Quotate() + ",";
        //    //    json += "\"qtyPending\":" + QuantityPendingAt(station) + ",";
        //    //    json += "\"qtyScrapped\":" + QuantityScrapped() + ",";
        //    //    json += "\"qtyCompleted\":" + QuantityCompleteAt(station) + ",";
        //    //    json += "\"members\":[";

        //    //    json += ExportTableRows(clientType, station);

        //    //    json += "]";
        //    //}
        //    //else if (clientType == "SupervisorClient")
        //    //{
        //    //    json += "\"stations\":";
        //    //    List<string> stations = new List<string>();
        //    //    if (m_station == StationClass.GetStation("Start") || QuantityPendingAt(m_station) > 0 || QuantityAt(m_station) > 0) stations.Add(m_station.Name);
        //    //    foreach (TravelerItem item in Items)
        //    //    {
        //    //        if (!stations.Exists(x => item.Station.Is(x)))
        //    //        {
        //    //            stations.Add(item.Station.Name);
        //    //        }
        //    //    }
        //    //    json += stations.Stringify();
        //    //}
        //    //json += "}";
        //    //return json;

        //}
        // export for JSON viewer
        public virtual string ExportHuman()
        {
            List<string> items = new List<string>();
            foreach (TravelerItem item in Items)
            {
                items.Add(item.ExportHuman());
            }
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"Date started", m_dateStarted.Quotate() },
                {"ID",m_ID.ToString() },
                {"Qty on traveler",m_quantity.ToString() },
                {"Orders",m_parentOrderNums.Stringify() },
                {"Items",items.Stringify(false) },
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
            header.Add("Orders");
            header.Add("Station");
            header.Add("Comment");
            return header.Stringify<string>().Trim('[').Trim(']');
        }
        // export for csv detail
        public virtual string ExportCSVdetail()
        {
            List<string> detail = new List<string>();
            detail.Add(m_ID.ToString());
            detail.Add(m_quantity.ToString());
            DateTime? soonestShipDate = SoonestShipDate;
            detail.Add(soonestShipDate.HasValue ? soonestShipDate.Value.ToString("MM/dd/yyyy") : "Make to stock");
            string orders = "";
            foreach (Order order in ParentOrders)
            {
                orders += "(" + order.FindItems(ID).Sum(i => i.QtyOrdered) + "  " + order.SalesOrderNo + " " + order.ShipDate.ToString("MM/dd/yyyy") + " " + order.CustomerNo + ")";
            }
            detail.Add(orders);
            detail.Add(m_station.Name);
            detail.Add(m_comment);
            return detail.Stringify<string>().Trim('[').Trim(']');
        }
        // export for operator view information
        public virtual string ExportTableRows(StationClass station)
        {
            Dictionary<string, string> obj = new Dictionary<string, string>();
            List<string> members = new List<string>();
            members.Add(new NameValueQty<string, int>("Part", ItemCode, Quantity).ToString());
            if (this is Part)
            {
                members.Add(new NameValueQty<string, string>("Description", (this as Part).Bill.BillDesc, "").ToString());
            }
            if (ParentOrders.Count > 0 )
            {
                foreach (Order order in ParentOrders)
                {
                    string customerString = order.SalesOrderNo + " - " + order.CustomerNo;
                    members.Add(new NameValueQty<string, int>("Order", customerString, order.FindItems(ID).Sum(i => i.QtyNeeded)).ToString());
                }
                //members.Add(new NameValueQty<string, string>("Customers", .Stringify(false).Trim('[').Trim(']').Replace(",","\\n"),"").ToString());
            }
            obj.Add("members", members.Stringify(false));
            return obj.Stringify();
        }
        public virtual Dictionary<string, Node> ExportViewProperties()
        {
            Dictionary<string, Node> list = new Dictionary<string, Node>();
            list.Add("Quantity", new TextNode(Quantity.ToString(),new Style("lime")));

            list.Add("Part", new TextNode(ItemCode,new Style("twoEM","red","shadow")));
            if (this is Part) list.Add("Description", new TextNode((this as Part).Bill.BillDesc));
            // orders
            if (ParentOrders.Any())
            {
                List<object> orders = new List<object>();
                foreach (Order order in ParentOrders)
                {
                    string customerString = "(" + order.FindItems(ID).Sum(i => i.QtyNeeded).ToString() + ") " + order.SalesOrderNo + " - " + order.CustomerNo;
                    orders.Add(customerString);
                }
                list.Add("Orders",ControlPanel.CreateList(orders));
            }
            // parents
            if (ParentTravelers.Any())
            {
                List<object> parents = new List<object>();
                foreach (Traveler parent in ParentTravelers)
                {
                    string parentString = parent.GetType().Name.Decompose() + " traveler: " + parent.PrintID() + " (" + parent.ItemCode + ")";
                    parents.Add(parentString);
                }
                list.Add(ParentTravelers.Count > 1 ? "Parents" : "Parent", ControlPanel.CreateList(parents));
            }
            // labor (if it exists)
            double labor = GetCurrentLabor(Station);
            TimeSpan rate = TimeSpan.FromMinutes(labor);
            string laborRate = "";
            if (rate.Minutes > 0) laborRate += rate.Minutes + " min";
            if (rate.Seconds > 0) laborRate += " " + rate.Seconds + " seconds";
            list.Add("Labor", new TextNode(labor > 0 ? laborRate : "No rate information"));
            if (m_comment != "") list.Add("Comment", ControlPanel.FormattedText(m_comment, new Style("orange", "shadow")));
            return list;

        }

        public virtual void Update(Form form)
        {

            // cannot update itemcode
            Quantity = Convert.ToInt32(form.ValueOf("quantity"));
            Comment = form.ValueOf("comment");
            Station = StationClass.GetStation(form.ValueOf("station"));
        }
        public virtual Form CreateForm()
        {
            Form form = new Form();
            form.Title = "Traveler";
            form.Integer("quantity", "Quantity",0);
            form.Textbox("comment", "Comment");
            form.Selection("station", "Starting Station", StationClass.StationNames());
            return form;
        }
        public virtual Form CreateFilledForm()
        {
            Form form = new Form();
            form.Title = "Traveler";
            form.Integer("quantity", "Quantity",m_quantity);
            form.Textbox("comment", "Comment",m_comment);
            form.Selection("station", "Starting Station",new List<string>(), m_station.Name);
            return form;
        }
        #endregion
        //--------------------------------------------------------
        #region Abstract Methods
        // finishes an item
        //public virtual void FinishItem(ushort ID)
        //{
        //    TravelerItem item = FindItem(ID);
        //    // now in post process
        //    item.State = ItemState.PostProcess;
        //    item.History.Add(new LogEvent(null, LogType.Finish, item.Station));
        //    // check to see if this concludes the traveler
        //    if (Items.Where(x => x.State == ItemState.PostProcess && !x.Scrapped).Count() >= m_quantity && Items.All(x => x.State == ItemState.PostProcess))
        //    {
        //        State = ItemState.PostProcess;
        //    }
        //}
        // returns true if the specified traveler can combine with this one
        public abstract bool CombinesWith(object[] args);
        
        // advances the item to the next station
        //public abstract void AdvanceItem(ushort ID, ITravelerManager travelerManager = null);
        // gets the next station for the given item
        public abstract StationClass GetNextStation(UInt16 itemID);
        // gets the work rate for the current station
        public abstract double GetCurrentLabor(StationClass station = null);
        // gets the total work wrapped up in the given station
        public abstract double GetTotalLabor(StationClass station = null);
        // overridden in derived classes, packs properties into the Export() json string
        public virtual Dictionary<string, string> ExportProperties(StationClass station = null)
        {
            return new Dictionary<string, string>()
            {
                {"forInventory",(m_parentOrders.Count == 0).ToString().ToLower()},
                {"qtyScrapped",Items.Count(i => i.Scrapped).ToString() }
            };
        }
        // returns true if this traveler is pending at the given station, else false
        public bool PendingAt(StationClass station)
        {
            //JsonObject config = (JsonObject)ConfigManager.GetJSON("stationTypes")[station.Type];
            //if (config.ContainsKey(this.GetType().Name))
            //{
            //    return StationClass.OfType(config[this.GetType().Name]["next"]);
            //}
            return QuantityPendingAt(station) > 0;
        }
        // pre
        public abstract void ImportInfo(ITravelerManager travelerManager, IOrderManager orderManager, OdbcConnection MAS);
        // get a list of fields from the label DB
        protected string GetLabelFields(List<string> fieldNames)
        {
            string json = "";
            // open the pack label database
            System.IO.StreamReader labelRef = new StreamReader(@"\\MGFS01\ZebraPrinter\data\databases\production.csv");
            string[] headerArray = labelRef.ReadLine().Split(',');

            string line = labelRef.ReadLine();
            while (line != "" && line != null)
            {
                string[] rowArray = line.Split(',');
                if (ItemCode.Contains(rowArray[0]))
                {
                    for (int index = 0; index < headerArray.Count(); index++)
                    {
                        if (fieldNames.Contains(headerArray[index]))
                        {
                            json += ',' + headerArray[index].Quotate() + ':' + rowArray[index].Quotate();
                        }
                    }
                    break;
                }

                line = labelRef.ReadLine();
            }
            return json;
        }
        #endregion
        //--------------------------------------------------------
        #region Private Methods
        protected double GetRate(Bill bill, StationClass station,bool total = false)
        {
            if (bill != null && bill.ComponentItems != null)
            {
                foreach (Item componentItem in bill.ComponentItems)
                {
                    double rate = GetRate(componentItem, station, total);
                    if (rate > 0.0) return rate;
                }
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
        protected int ScrapSequenceNo(TravelerItem item)
        {
            ScrapEvent scrapEvent = (ScrapEvent)item.History.FirstOrDefault(x => x is ScrapEvent);
            // returns the quantity of items that have a scrap event at or prior to this item's scrap event
            List<TravelerItem> items  =  Items.Where(x => x.History.Exists(y => y is ScrapEvent && y.Date <= scrapEvent.Date)).ToList();
            return items.Count;
        }
        #endregion
        //--------------------------------------------------------
        #region Properties

        // general
        protected int m_ID;
        private string m_itemCode;
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
        private GlobalItemState m_state;
        private string m_dateStarted;
        private int m_priority;
        private ushort m_lastReworkAccountedFor;
       
        #endregion
        //--------------------------------------------------------
        #region Interface
        public int ID
        {
            get
            {
                return m_ID;
            }
        }

        

        public int Quantity
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

        

        public StationClass Station
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

        public List<string> ParentOrderNums
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

        public GlobalItemState State
        {
            get
            {
                return m_state;
            }
            private set
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

        public List<Traveler> ParentTravelers
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

        public List<Traveler> ChildTravelers
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

        public List<Order> ParentOrders
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
        public DateTime? SoonestShipDate
        {
            get
            {
                return ParentOrders.Count > 0  ? new DateTime?(ParentOrders.Min(y => y.ShipDate)) : null;
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


        public string ItemCode
        {
            get
            {
                return m_itemCode;
            }
            protected set
            {
                m_itemCode = value;
            }
        }

        public ushort LastReworkAccountedFor
        {
            get
            {
                return m_lastReworkAccountedFor;
            }

            set
            {
                m_lastReworkAccountedFor = value;
            }
        }
        #endregion
    }
}
