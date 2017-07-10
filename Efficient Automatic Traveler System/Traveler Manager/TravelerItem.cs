using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    public enum LocalItemState
    {
        PreProcess,
        InProcess,
        PostProcess
    }
    public enum GlobalItemState
    {
        PreProcess,
        InProcess,
        Scrapped,
        PendingRework,
        Finished
    }
    public class TravelerItem
    {
        public TravelerItem(string itemCode, UInt16 ID, UInt16 sequenceNo,StationClass station, bool replacement = false)
        {
            m_ID = ID;
            m_itemCode = itemCode;
            m_sequenceNo = sequenceNo;
            m_replacement = replacement;
            m_station = station;
            m_lastStation = StationClass.GetStation("Start");
            m_history = new List<Event>();
            m_order = null;
            m_localState = LocalItemState.PreProcess;
            GlobalState = GlobalItemState.InProcess;
            m_comment = "";
        }
        public TravelerItem(string json)
        {
            try
            {
                JsonObject obj = (JsonObject)JSON.Parse(json);
                if (obj.ContainsKey("ID")) ID = Convert.ToUInt16((int)obj["ID"]);
                if (obj.ContainsKey("sequenceNo")) SequenceNo = Convert.ToUInt16((int)obj["sequenceNo"]);
                if (obj.ContainsKey("replacement")) Replacement = obj["replacement"];

                // Convert old DB
                if (obj.ContainsKey("scrapped") && obj["scrapped"])
                {
                    GlobalState = GlobalItemState.Scrapped;
                } else if (obj.ContainsKey("globalState"))
                {
                    GlobalState = obj["globalState"].ToEnum<GlobalItemState>();
                } else
                {
                    GlobalState = GlobalItemState.InProcess;
                }
                if (obj.ContainsKey("station")) Station = StationClass.GetStation(obj["station"]);
                if (obj.ContainsKey("itemCode")) ItemCode = obj["itemCode"];
                if (obj.ContainsKey("lastStation")) LastStation = StationClass.GetStation(obj["lastStation"]);
                
                
                //m_order = Server.OrderManager.FindOrder(obj["order"]);
                History = new List<Event>();
                if (obj.ContainsKey("history"))
                {
                    foreach (string eventString in (new StringStream(obj["history"])).ParseJSONarray())
                    {
                        History.Add(BackupManager.ImportDerived<Event>(eventString));
                    }
                }
                if (History.OfType<LogEvent>().ToList().Exists(e => e.LogType == LogType.Finish)) GlobalState = GlobalItemState.Finished;
                if (obj.ContainsKey("state")) LocalState = obj["state"].ToEnum<LocalItemState>();
                if (obj.ContainsKey("comment")) Comment = obj["comment"];
            }
            catch (Exception ex)
            {
                Server.WriteLine("Problem when reading TravelerItem from file: " + ex.Message + "; StackTrace: " + ex.StackTrace);
            }
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"ID",m_ID.ToString()},
                {"sequenceNo",m_sequenceNo.ToString() },
                {"replacement", m_replacement.ToString().ToLower() },
                {"globalState", GlobalState.ToString().Quotate()},
                {"station",Station.Name.Quotate() },
                {"itemCode",m_itemCode.Quotate() },
                {"lastStation",m_lastStation.Name.Quotate() },
                {"history",m_history.ToList<Event>().Stringify<Event>() },
                {"order",(m_order != null ? m_order.SalesOrderNo : "").Quotate() },
                {"state",m_localState.ToString().Quotate() }
            };
            if (m_comment != "") obj.Add("comment", m_comment.Quotate());
            return obj.Stringify();
        }
        public bool PendingAt(StationClass station)
        {
            if (LocalState == LocalItemState.PreProcess && Station == station)
            {
                return true;
            }
            else if (LocalState == LocalItemState.PostProcess)
            {
                return station.PreRequisites(Parent).Contains(Station);
            } else
            {
                return false;
            }
            
            //// get the last station that this is complete at
            //StationClass lastStation = null;
            //// Get the last k
            //if (History.Any()) {
            //    History.OfType<LogEvent>().LastOrDefault(e => e.LogType == LogType.Rework).Station;
            //    // if no reworks were found, determine station from completed events
            //    if (lastStation == null)
            //    {
            //        lastStation
            //    }
            //} else
            //{
            //}
        }
        public bool InProcessAt(StationClass station)
        {
            return LocalState == LocalItemState.InProcess && Station == station;
        }
        public bool CompleteAt(StationClass station)
        {
            return LocalState == LocalItemState.PostProcess && Station == station;
        }
        public string ExportTableRows(StationClass station)
        {
            List<string> members = new List<string>();
            if (m_comment.Length > 0) members.Add(new NameValueQty<string, string>("Comment", m_comment,"").ToString());
            return members.Stringify(false);
        }
        public virtual Dictionary<string, Node> ExportViewProperties()
        {
            Dictionary<string, Node> list = new Dictionary<string, Node>();
            if (m_comment != "") list.Add("Comment", ControlPanel.FormattedText(m_comment, new Style("orange", "shadow")));
            return list;
        }
        public string ExportHuman()
        {
            List<string> history = new List<string>();
            foreach (Event evt in m_history)
            {
                history.Add(evt.ExportHuman());
            }
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"ID",m_ID.ToString()},
                {"Sequence No",m_sequenceNo.ToString() },
                {"Replacement", m_replacement.ToString().ToLower() },
                {"Global State",GlobalState.ToString().Quotate()},
                {"Station",Station.Name.Quotate() },
                {"ItemCode",m_itemCode.Quotate() },
                {"Last station",m_lastStation.Name.Quotate() },
                {"History",history.Stringify(false) },
                {"Order",(m_order != null ? m_order.SalesOrderNo : "").Quotate() },
                {"State",m_localState.ToString().Quotate() }
            };
            return obj.Stringify();
        }
        public double ProcessTimeAt(StationClass station)
        {
            return m_history.OfType<ProcessEvent>().ToList().Where(evt => evt.Station == station).Sum(e => e.Duration);
        }
        public void AssignOrder()
        {
            Parent.ParentOrders.Sort((a, b) => a.ShipDate.CompareTo(b.ShipDate)); // sort in ascending order (soonest first)

            foreach (Order parent in Parent.ParentOrders)
            {
                List<OrderItem> orderItems = parent.FindItems(ID); // the items that apply to this traveler

                int qtyOrdered = Parent.QuantityOrdered();
                // If there are less items assigned to that order than what was ordered (takes into account multiple order items that match the traveler)
                foreach (OrderItem orderItem in orderItems)
                {
                    if (orderItem.QtyOnHand < orderItem.QtyOrdered)
                    {
                        // assign this order to the item
                        Order = parent;

                        // allocate this item on the order
                        // INVENTORY
                        //orderItem.QtyOnHand++;

                    }
                }


                //if (traveler.Items.Where(x => x.Order == order.SalesOrderNo).Count() < orderItems.Sum(x => x.QtyOrdered))
                //{

                //}
            }
        }
        public void Start(User user, StationClass station)
        {
            LocalState = LocalItemState.InProcess;
            GlobalState = GlobalItemState.InProcess;
            Station = station;
            History.Add(new ProcessEvent(user, station, 0, ProcessType.Started));
            
            Server.TravelerManager.OnTravelersChanged(Parent);
        }
        public void Complete(User user, StationClass station, double duration = 0.0)
        {
            LocalState = LocalItemState.PostProcess;
            // remove the start event

            if (Started(station))
            {
                ProcessEvent startEvent = History.OfType<ProcessEvent>().First(e => e.Process == ProcessType.Started);

                TimeSpan timeSpan = DateTime.Now - startEvent.Date; // difference between start and now

                History.Add(new ProcessEvent(user, m_station, timeSpan.TotalMinutes, ProcessType.Completed));
                History.Remove(startEvent);
            }
            else
            {
                History.Add(new ProcessEvent(user, m_station, duration, ProcessType.Completed));
            }
            // Finish this item if this station is configured to finish the item
            if (station.Finishes(Parent))
            {
                Finish();
            }
            Server.TravelerManager.OnTravelersChanged(Parent);
        }
        public void Scrap(User user, StationClass station)
        {
            LocalState = LocalItemState.PostProcess;
            GlobalState = GlobalItemState.Scrapped;
            Station = StationClass.GetStation("Scrapped");

            // Notify everyone who wants to be notified
            Server.NotificationManager.PushNotification("Scrap", Summary.HumanizeDictionary(Summary.ScrapDetail(Parent, this)));
            // print le label
            Parent.PrintLabel(ID, LabelType.Scrap);
            Server.TravelerManager.OnTravelersChanged(Parent);
        }
        public void Rework(User user, StationClass station)
        {
            LocalState = LocalItemState.PreProcess;
            GlobalState = GlobalItemState.InProcess;
            Station = StationClass.GetStation("Rework");
            History.Add(new LogEvent(user, LogType.Rework, station));
            Station = station;
            Server.TravelerManager.OnTravelersChanged(Parent);
        }
        public void FlagRework(User user, StationClass station, Form form)
        {
            GlobalState = GlobalItemState.PendingRework;

            History.Add(new Documentation(user, LogType.FlagRework, station, form.ToJSON()));
            Server.TravelerManager.OnTravelersChanged(Parent);
        }
        public void DeflagRework(User user, StationClass station, Form form)
        {
            // Finish this item if its next station is finished
            if (Parent.PendingAt(StationClass.GetStation("Finished")))
            {
                GlobalState = GlobalItemState.Finished;
            } else if (History.OfType<ScrapEvent>().Any())
            {
                GlobalState = GlobalItemState.Scrapped;
            }
            {
                GlobalState = GlobalItemState.InProcess;
            }

            History.Add(new Documentation(user, LogType.DeflagRework, station, form.ToJSON()));
            Server.TravelerManager.OnTravelersChanged(Parent);
        }
        public void Finish()
        {
            LocalState = LocalItemState.PostProcess;
            GlobalState = GlobalItemState.Finished;
            Station = StationClass.GetStation("Finished");
            // check to see if the traveler is finished
            if (Parent.Items.Count(i => i.GlobalState == GlobalItemState.Finished) >= Parent.Quantity)
            {
                Parent.Finish();
            }
            // add this item to inventory
            InventoryManager.Add(ItemCode);
            Server.TravelerManager.OnTravelersChanged(Parent);
        }
        public void Undo()
        {

        }
        // Properties
        private UInt16 m_ID;
        private UInt16 m_sequenceNo;
        private bool m_replacement;
        private StationClass m_station;
        private string m_itemCode;
        private StationClass m_lastStation;
        private List<Event> m_history;
        private Order m_order;
        private LocalItemState m_localState;
        private GlobalItemState m_globalState;
        private Traveler m_parent;
        private string m_comment = "";
        private bool m_cartonPrinted = false;

        public ushort ID
        {
            get
            {
                return m_ID;
            }
            private set
            {
                m_ID = value;
            }
        }

        public StationClass Station
        {
            get
            {
                return m_station;
            }

            private set
            {
                m_lastStation = m_station;
                m_station = value;
            }
        }

        public StationClass LastStation
        {
            get
            {
                return m_lastStation;
            }

            set
            {
                m_lastStation = value;
            }
        }

        public bool Scrapped
        {
            get
            {
                return GlobalState == GlobalItemState.Scrapped;
            }
        }
        public bool Finished
        {
            get
            {
                return GlobalState == GlobalItemState.Finished;
            }
        }
        public bool BeenCompletedDuring(StationClass station, DateTime date)
        {
            return History.OfType<ProcessEvent>().ToList().Exists(e => e.Date.Day == date.Date.Day) && BeenCompleted(station);
        }
        public bool BeenCompletedDuring(List<StationClass> stations, DateTime date)
        {
            return stations.All(station => History.OfType<ProcessEvent>().ToList().Exists(e => e.Date.Day == date.Date.Day) && BeenCompleted(station));
        }
        public bool PendingRework
        {
            get
            {
                return GlobalState == GlobalItemState.PendingRework;
            }
        }
        public bool Started(StationClass station)
        {
            return History.OfType<ProcessEvent>().ToList().Exists(e => e.Process == ProcessType.Started && e.Station == m_station);
        }
        public List<Event> History
        {
            get
            {
                return m_history;
            }

            set
            {
                m_history = value;
            }
        }

        public Order Order
        {
            get
            {
                return m_order;
            }

            set
            {
                m_order = value;
            }
        }

        public LocalItemState LocalState
        {
            get
            {
                return m_localState;
            }
            private set
            {
                m_localState = value;
            }
        }

        public ushort SequenceNo
        {
            get
            {
                return m_sequenceNo;
            }

            set
            {
                m_sequenceNo = value;
            }
        }

        public bool Replacement
        {
            get
            {
                return m_replacement;
            }

            set
            {
                m_replacement = value;
            }
        }

        public string ItemCode
        {
            get
            {
                return m_itemCode;
            }

            set
            {
                m_itemCode = value;
            }
        }

        public Traveler Parent
        {
            get
            {
                return m_parent;
            }

            set
            {
                m_parent = value;
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

        public bool CartonPrinted
        {
            get
            {
                return m_cartonPrinted;
            }

            set
            {
                m_cartonPrinted = value;
            }
        }

        public GlobalItemState GlobalState
        {
            get
            {
                return m_globalState;
            }

            private set
            {
                m_globalState = value;
            }
        }

        public bool BeenCompleted(StationClass station)
        {
            // negate any events that a rework event dismisses
            // latest rework station
            List<ProcessEvent> applicableHistory = History.OfType<ProcessEvent>().ToList();
            if (History.Any())
            {
                LogEvent lastRework = History.OfType<LogEvent>().LastOrDefault(e => e.LogType == LogType.Rework);
                // if reworks were found, eliminate all events for the latest rework station
                if (lastRework != null)
                {
                    applicableHistory.RemoveAll(e => e.Date >= lastRework.Date);
                }
            }
            foreach (ProcessEvent evt in applicableHistory)
            {
                if (evt.Station.Type == station.Type && evt.Process == ProcessType.Completed)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
