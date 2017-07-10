using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

using System.Net.Sockets;
using System.Net;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

using System.Timers;
using System.Data;

namespace Efficient_Automatic_Traveler_System
{
    class OperatorClient : Client, IOperator, ITravelers
    {
        //------------------------------
        // Public members
        //------------------------------
        public OperatorClient (TcpClient client, ITravelerManager travelerManager) : base(client)
        {
            AccessLevel = AccessLevel.Operator;
            m_travelerManager = travelerManager;
            m_partTimer = new ClientStopwatch(this);
            m_stationTimer = new ClientStopwatch(this);
            m_searchedItem = null;
            SendMessage((new ClientMessage("InitStations", StationClass.GetStations().Stringify())).ToString());
        }

        public string SetStation(string json)
        {
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                m_station = StationClass.GetStation(obj["station"]);
                m_stationTimer.Clear("ClearStationTimer");
                m_stationTimer.Start("StartStationTimer");

                HandleTravelersChanged();
                if (m_station.Mode == StationMode.Serial)
                {
                    HideSubmitBtn();
                } else if (m_station.Mode == StationMode.Batch)
                {
                    ShowSubmitBtn();
                }
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
            }
            return "";
        }
        public void HandleTravelersChanged()
        {
            if (m_station != null)
            {
                Dictionary<string, string> message = new Dictionary<string, string>();
                //// PreProcess
                //List<string> travelerStrings = new List<string>();

                //foreach (Traveler traveler in m_travelerManager.GetTravelers.Where(x => x.State == ItemState.InProcess && (x.QuantityPendingAt(m_station) > 0 || x.QuantityAt(m_station) > 0)).ToList())
                //{
                //    travelerStrings.Add(ExportTraveler(traveler));
                //}

                //message.Add("preProcess", travelerStrings.Stringify(false));
                //// InProcess
                //travelerStrings.Clear();

                //foreach (Traveler traveler in m_travelerManager.GetTravelers.Where(x => x.State == ItemState.InProcess && (x.QuantityPendingAt(m_station) > 0 || x.QuantityAt(m_station) > 0)).ToList())
                //{
                //    foreach (TravelerItem item in traveler.Items.Where(i => i.State == ItemState.InProcess && i.Station == m_station))
                //    {
                //        travelerStrings.Add(ExportTravelerItem(traveler,item));
                //    }
                //}

                //message.Add("inProcess", travelerStrings.Stringify(false));

                //message.Add("mirror", mirror.ToString().ToLower());
                //SendMessage(new ClientMessage("HandleTravelersChanged", message.Stringify(), "LoadCurrent").ToString());

                // PreProcess traveler queue items
                // preprocess for this station are all PostProcess items sitting at a station that pushes items to this type of station

                NodeList preProcess = CreateTravelerQueue(m_travelerManager.GetTravelers.Where(t => t.QuantityPendingAt(m_station) > 0).ToList(), m_station, false);
                ControlPanel preProcessControlPanel = new ControlPanel("preProcess", preProcess, "preProcessQueue");
                SendMessage(preProcessControlPanel.Dispatch().ToString());


                List<TravelerItem> items = new List<TravelerItem>();
                // InProcess queue items
                items = m_travelerManager.GetTravelers.SelectMany(t => t.Items.Where(i => i.InProcessAt(m_station))).ToList();

                // sort the items by start event time (most recent on top)
                try
                {
                    if (items.Count(i => i.History.OfType<ProcessEvent>().ToList().Exists(e => e.Process == ProcessType.Started && e.Station == m_station)) >= 2)
                    {
                        items.Sort((a, b) => a.History.Last().Date.CompareTo(b.History.Last().Date));
                    }
                }
                catch (Exception ex) { }
                //else if (m_station.Mode == StationMode.Batch)
                //{
                //    // PostProcess queue items
                //    items = m_travelerManager.GetTravelers.SelectMany(t => t.Items.Where(i => i.Station == m_station && !i.Scrapped && i.LocalState == LocalItemState.PostProcess)).ToList();

                //    // sort the items by completed event time (most recent on top)
                //    try
                //    {
                //        if (items.Any()) items.Sort((a, b) => a.History.OfType<ProcessEvent>().First(e => e.Process == ProcessType.Completed).Date.CompareTo(b.History.OfType<ProcessEvent>().First(e => e.Process == ProcessType.Completed).Date));
                //    }
                //    catch (Exception ex) { }
                //}
                if (m_item != null && !items.Contains(m_item)) ClearTravelerView();

                NodeList inProcess = CreateItemQueue(items);
                ControlPanel cp = new ControlPanel("inProcess", inProcess, "inProcessQueue");
                SendMessage(cp.Dispatch().ToString());


                UpdateUI();
            }
        }
        protected override Row CreateTravelerQueueItem(GlobalItemState state, Traveler traveler)
        {
            Row queueItem = base.CreateTravelerQueueItem(state, traveler);
            if (m_current == traveler) queueItem.Style += new Style("selected");
            return queueItem;
        }
        protected override Row CreateItemQueueItem(GlobalItemState state, TravelerItem item)
        {
            Row queueItem = base.CreateItemQueueItem(state, item);
            if (item == m_item) queueItem.Style += new Style("selected");
            return queueItem;
        }
        // Direct UI control vvvvvvvvvvvvvvvvvvvvvvvvvv
        private void UpdateUI()
        {
            if (m_current == null || (m_item == null && !m_station.CreatesThis(m_current)))
            {
                SetQtyCompleted(0);
                SetQtyPending(0);
                DisableProcessBtns();
                DisableDrawingBtn();
                DisableMoreInfoBtn();
                DisableCommentBtn();
                ClearTravelerView();
                m_partTimer.Clear("ClearPartTimer");
            } else
            {
                EnableMoreInfoBtn();
                if (m_item != null)
                {
                    EnableCommentBtn();
                }
                if (m_current is Table)
                {
                    EnableDrawingBtn();
                }
                int qtyPending = m_current.QuantityPendingAt(m_station);
                SetQtyPending(qtyPending);
                //int qtyCompleted = m_current.QuantityCompleteAt(m_station);
                //SetQtyCompleted(qtyCompleted);

                if (m_item != null || (m_current != null && m_station.CreatesThis(m_current)))
                {
                    EnableProcessBtns();
                    m_partTimer.Resume();
                } else
                {
                    DisableProcessBtns();
                }

                LoadTravelerView(m_current,m_item);
            }
        }
        private void DisableProcessBtns()
        {
            SendMessage(new ClientMessage("DisableUI").ToString());
        }
        private void HideSubmitBtn()
        {
            SendMessage(new ClientMessage("HideSubmitBtn").ToString());
        }
        private void DisableMoreInfoBtn()
        {
            SendMessage(new ClientMessage("DisableMoreInfoBtn").ToString());
        }
        private void DisableDrawingBtn()
        {
            SendMessage(new ClientMessage("DisableDrawingBtn").ToString());
        }
        private void DisableCommentBtn()
        {
            SendMessage(new ClientMessage("DisableCommentBtn").ToString());
        }
        private void EnableProcessBtns()
        {
            SendMessage(new ClientMessage("EnableUI").ToString());
        }
        private void ShowSubmitBtn()
        {
            SendMessage(new ClientMessage("ShowSubmitBtn").ToString());
        }
        private void EnableMoreInfoBtn()
        {
            SendMessage(new ClientMessage("EnableMoreInfoBtn").ToString());
        }
        private void EnableDrawingBtn()
        {
            SendMessage(new ClientMessage("EnableDrawingBtn").ToString());
        }
        private void EnableCommentBtn()
        {
            SendMessage(new ClientMessage("EnableCommentBtn").ToString());
        }
        private void SetQtyPending(int qty)
        {
            SendMessage(new ClientMessage("SetQtyPending",qty.ToString()).ToString());
        }
        private void SetQtyCompleted(int qty)
        {
            SendMessage(new ClientMessage("SetQtyCompleted", qty.ToString()).ToString());
        }
        private void LoadTravelerView(Traveler traveler, TravelerItem item = null)
        {
            Dictionary<string, string> obj = new Dictionary<string, string>();
            obj.Add("ID", (item != null ? traveler.PrintSequenceID(item) : traveler.ID.ToString("D6")).Quotate());
            if (item != null) obj.Add("itemMembers", item.ExportTableRows(m_station));

            Column travelerView = new Column();
            travelerView.Style.AddStyle("width", "100%");
            travelerView.Style.AddStyle("overflow-x", "hidden");
            // traveler ID
            TextNode travelerID = new TextNode(traveler.PrintSequenceID(item), new Style("yellow"));
            travelerView.Add(travelerID);
            // table
            Node viewTable = ControlPanel.CreateDictionary(traveler.ExportViewProperties());
            viewTable.Style.AddStyle("width", "100%");
            travelerView.Add(viewTable);

            // item table
            if (item != null)
            {
                Dictionary<string, Node> itemProperties = item.ExportViewProperties();
                if (itemProperties.Any())
                {

                    // item table title
                    TextNode itemTitle = new TextNode("Item specific", new Style("yellow"));
                    travelerView.Add(itemTitle);

                    Node itemTable = ControlPanel.CreateDictionary(itemProperties);
                    itemTable.Style.AddStyle("width", "100%");
                    travelerView.Add(itemTable);
                }
            }
            // buttons
            if (m_station.Type == "tablePack")
            {
                // print carton labels -_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_
                Dictionary<string, string> printCarton = new Dictionary<string, string>() {
                    {"travelerID",m_current.ID.ToString() },
                    {"itemID",m_item.ID.ToString() },
                    {"labelType",LabelType.Pack.ToString().Quotate() }
                };

                travelerView.Add(new Button("Print Carton Labels", "PrintLabel",printCarton.Stringify()));
                // print table label -_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_
                Dictionary<string, string> printTable = new Dictionary<string, string>() {
                    {"travelerID",m_current.ID.ToString() },
                    {"itemID",m_item.ID.ToString() },
                    {"labelType",LabelType.Table.ToString().Quotate() }
                };
                travelerView.Add(new Button("Print Table label", "PrintLabel",printTable.Stringify()));
            }
            // View Drawing -_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_
            if (m_current is Part && (m_current as Part).HasDrawing())
            {
                travelerView.Add(new Button("Drawing", "OpenDrawing"));
            }
            if (m_item != null)
            {
                // Print Tracking label -_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_
                Dictionary<string, string> printTracking = new Dictionary<string, string>() {
                    {"travelerID",m_current.ID.ToString() },
                    {"itemID",m_item.ID.ToString() },
                    {"labelType",LabelType.Tracking.ToString().Quotate() }
                };
                travelerView.Add(new Button("Print Tracking label", "PrintLabel", printTracking.Stringify()));
                // -_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_
            }
            // More Info -_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_
            travelerView.Add(new Button("Traveler Information", "LoadTravelerJSON"));
            // -_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_

            // Add Comment -_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_
            travelerView.Add(new Button("Add Comment", "AddComment"));
            // -_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_

            ControlPanel travelerViewCP = new ControlPanel("travelerView", travelerView, "viewContainer");

            SendMessage(travelerViewCP.Dispatch().ToString());

            //SendMessage(new ClientMessage("LoadTravelerView", obj.Stringify().MergeJSON(traveler.ExportTableRows(m_station))).ToString());
        }
        private void ClearTravelerView()
        {
            SendMessage(new ClientMessage("ClearTravelerView").ToString());
        }
        //^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
        private void NewPartStarted()
        {
            m_partTimer.Clear("ClearPartTimer");
            if (m_current.QuantityPendingAt(m_station) > 0) m_partTimer.CountDown(m_current.GetCurrentLabor(m_station), "CountdownPartTimer");
        }
        private void LoadTimerFor(TravelerItem item)
        {
            // start countdown from current labor minus elapsed time since the part was started
            if (m_current.QuantityPendingAt(m_station) > 0) m_partTimer.CountDown(m_current.GetCurrentLabor(m_station) - (DateTime.Now - GetStartEvent(item).Date).TotalMinutes , "CountdownPartTimer");
        }
        private ProcessEvent GetStartEvent(TravelerItem item)
        {
            return item.History.OfType<ProcessEvent>().First(e => e.Process == ProcessType.Started);
        }
        //private string ExportTraveler(Traveler traveler)
        //{
        //    string travelerJSON = traveler.ToString();
        //    Dictionary<string, string> queueItem = new Dictionary<string, string>();
        //    queueItem.Add("queueItem", traveler.ExportStationSummary(m_station));
        //    travelerJSON = travelerJSON.MergeJSON(queueItem.Stringify()); // merge station properties
        //    travelerJSON = travelerJSON.MergeJSON(traveler.ExportTableRows(m_station));
        //    travelerJSON = travelerJSON.MergeJSON(traveler.ExportProperties(m_station).Stringify());
        //    return travelerJSON;
        //}
        public string ExportTravelerItem(Traveler traveler, TravelerItem item)
        {
            string itemJSON = item.ToString();
            itemJSON = itemJSON.MergeJSON(traveler.ExportTableRows(m_station));
            Dictionary<string, string> extraProps = new Dictionary<string, string>()
            {
                {"sequenceID",traveler.PrintSequenceID(item).Quotate() },
                {"travelerID",traveler.ID.ToString() }
            };
            itemJSON = itemJSON.MergeJSON(extraProps.Stringify());
            itemJSON = itemJSON.MergeJSON(traveler.ExportProperties(m_station).Stringify());
            return itemJSON;
        }
        public ClientMessage ChecklistSubmit(string json)
        {
            m_partStart = DateTime.Now;
            NewPartStarted();
            return new ClientMessage();
        }
        //------------------------------
        // Private members
        //------------------------------
        private void DisplayChecklist()
        {
            try
            {
                Dictionary<string, string> stationType = new StringStream(new StringStream(ConfigManager.Get("stationTypes")).ParseJSON()[m_station.Type]).ParseJSON();
                if (stationType.ContainsKey("checklist"))
                {
                    string list = stationType["checklist"];
                    ClientMessage message = new ClientMessage("DisplayChecklist", list);
                    SendMessage(message.ToString());
                }
            } catch (Exception ex)
            {
                Server.LogException(ex);
            }
        }
        //------------------------------
        // Properties
        //------------------------------
        protected ITravelerManager m_travelerManager;
        protected StationClass m_station;
        protected Traveler m_current;
        protected TravelerItem m_item;
        protected DateTime m_partStart;
        protected ClientStopwatch m_partTimer;
        protected ClientStopwatch m_stationTimer;
        private TravelerItem m_searchedItem;
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
        //----------
        // Events
        //----------
        public event TravelersChangedSubscriber TravelersChanged;
        // JS client interface (these are the properties visible to the js interface calling system)
        public List<Traveler> GetTravelers
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        protected TravelerItem SearchedItem
        {
            get
            {
                return m_searchedItem;
            }

            set
            {
                m_searchedItem = value;
            }
        }

        public override ClientMessage Login(string json)
        {
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                ClientMessage message = base.Login(json);
                if (message.Method == "LoginSuccess" && obj.ContainsKey("station"))
                {
                    SetStation(json);
                    Dictionary<string, string> paramObj = new Dictionary<string, string>()
                    {
                        {"user",message.Parameters },
                        {"station",obj["station"].Quotate()}
                    };
                    return new ClientMessage("LoginSuccess", paramObj.Stringify());
                }
                else
                {
                    return message;
                }
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
                return new ClientMessage("LoginPopup", ("System error! oops...").Quotate());
            }
        }
        public ClientMessage CompleteItem(string json = "")
        {
            try
            {
                TravelerItem item = m_item;
                m_item = null;
                // deselect the selected item
                if (item == null && m_station.CreatesThis(m_current))
                {
                    item = m_current.AddItem(m_station);
                }
                
                item.Complete(m_user, m_station, m_partTimer.Stopwatch.Elapsed.TotalMinutes);
                UpdateUI();
                NewPartStarted(); // timers
                //if (m_station.Mode == StationMode.Serial)
                //{
                //    SubmitTraveler("");
                //}
                return new ClientMessage();
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error completing item");
            }
        }
        //public ClientMessage AddTravelerEvent(string json)
        //{
        //    try
        //    {
        //        Dictionary<string, string> obj = new StringStream(json).ParseJSON();
        //        Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
        //        //traveler.GetCurrentLabor(m_station) - Convert.ToDouble(obj["time"])
        //        DateTime now = DateTime.Now;
        //        TimeSpan duration = now.Subtract(m_partStart);
        //        m_partStart = now;
        //        ProcessEvent evt = new ProcessEvent(m_user, m_station, duration.TotalMinutes, (ProcessType)Enum.Parse(typeof(ProcessType), obj["eventType"]));
                
        //        TravelerItem item = (obj["itemID"] != "undefined" ? traveler.FindItem(Convert.ToUInt16(obj["itemID"])) : null);
        //        if (item != null && evt.Process == ProcessType.Completed)
        //        {
        //            // remove the start event
        //            item.History.RemoveAll(e => e is ProcessEvent && (e as ProcessEvent).Process == ProcessType.Started);
        //        }
        //        SendMessage( m_travelerManager.AddTravelerEvent(evt,traveler,item).ToString());
        //        UpdateUI();
        //        return new ClientMessage();
        //    } catch (Exception ex)
        //    {
        //        Server.LogException(ex);
        //        return new ClientMessage("Info", "Error occured");
        //    }
        //}
        public ClientMessage ScrapEvent(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                TravelerItem item = m_item;
                m_item = null;
                TimeSpan duration = m_partTimer.Stopwatch.Elapsed;
                ScrapEvent evt = new ScrapEvent(m_user, m_station, duration.TotalMinutes, Convert.ToBoolean(obj["startedWork"].ToLower()),obj["source"],obj["reason"]);
                item.Scrap(m_user, m_station);
                ClientMessage message = new ClientMessage();
                int userScrapQty = m_travelerManager.GetTravelers.Sum(t => t.Items.Count(i => i.History.OfType<ScrapEvent>().ToList().Exists(e => e.User.UID == m_user.UID && e.Date >= DateTime.Today)));
                message.Parameters = (message.Parameters.DeQuote() + "<br>You have scrapped " + userScrapQty + " items today").Quotate();
                if (userScrapQty >= 5)
                {
                    if (userScrapQty >= 10)
                    {
                        if (userScrapQty >= 15)
                        {
                            message.Parameters = (message.Parameters.DeQuote() +  "<br>You have scrapped too many items for your own good").Quotate();
                        } else
                        {
                            message.Parameters = (message.Parameters.DeQuote() + "<br>Please stop scrapping so many items!").Quotate();
                        }
                    } else
                    {
                        message.Parameters = (message.Parameters.DeQuote() + "<br>You should think about not scrapping so much").Quotate();
                    }
                }
               
                NewPartStarted();
                return message;
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error occured");
            }
        }
        public ClientMessage DisplayScrapReport(string json)
        {
            try
            {
                m_partTimer.Stop("StopPartTimer");
                string scrapReport = ConfigManager.Get("scrapReport");
                return new ClientMessage("DisplayScrapReport", scrapReport);
            } catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error occured");
            }
        }
        //public ClientMessage SubmitTraveler(string json)
        //{
        //    try
        //    {
        //        AddHistory(new Dictionary<string, object>() { { "traveler", m_current }, { "items", m_current.CompletedItems(m_station) } });
        //        m_item = null;
        //        SendMessage( m_travelerManager.SubmitTraveler(
        //            m_current,
        //            m_station
        //        ).ToString());
        //        UpdateUI();
        //        return new ClientMessage();
        //    }
        //    catch (Exception ex)
        //    {
        //        Server.LogException(ex);
        //        return new ClientMessage("Info","Error occured");
        //    }
        //}
        // Button Click events
        public ClientMessage OpenDrawing(string json)
        {
            try
            {
                return new ClientMessage("Redirect", ("../drawings/" + (m_current as Part).Bill.DrawingNo.Split('-')[0] + ".pdf").Quotate());
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error occured");
            }
        }
        //public ClientMessage LoadTraveler(string json)
        //{
        //    Traveler freshTraveler = m_travelerManager.FindTraveler(Convert.ToInt32(new StringStream(json).ParseJSON()["travelerID"]));
        //    if (freshTraveler != null && ( m_current == null || freshTraveler.ID != m_current.ID))
        //    {
        //        DisplayChecklist();
        //        // auto-submit completed items
        //        m_travelerManager.SubmitTraveler(m_current, m_station);
        //    }
        //    m_current = freshTraveler;
            
        //    return new ClientMessage();
        //}
        public ClientMessage LoadTravelerJSON(string json)
        {
            return new ClientMessage("LoadTravelerJSON", m_current.ExportHuman());
        }
        public ClientMessage AddComment(string json)
        {
            try
            {
                Form form = new Form();
                form.Title = "Add Comment";
                form.Textbox("comment", "Comment");
                form.Selection("target", "Target", new List<string>() { "traveler", "item" },"item");
                return form.Dispatch("CommentSubmitted");
            } catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error opening comment dialog");
            }
        }
        public ClientMessage CommentSubmitted(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON(false);
                Form form = new Form(obj["form"]);
                if (form.ValueOf("target") == "item" && m_item != null)
                {
                    m_item.Comment +=
                      (m_item.Comment.Length > 0 ? "\n" : "") +
                      m_user.Name + " ~ " + form.ValueOf("comment");
                } else if (form.ValueOf("target") == "traveler" && m_current != null)
                {
                    m_current.Comment +=
                     (m_current.Comment.Length > 0 ? "\n" : "") +
                     m_user.Name + " ~ " + form.ValueOf("comment");
                }
                m_travelerManager.OnTravelersChanged(new List<Traveler>() { m_current });
                return new ClientMessage();
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error submitting comment");
            }
        }
        public ClientMessage LoadTraveler(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                return LoadTraveler(traveler);
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error loading traveler");
            }
        }
        private ClientMessage LoadTraveler(Traveler traveler)
        {
            m_item = null;
            if (m_current == null || (traveler != null && traveler.ID != m_current.ID))
            {
                if (traveler.CurrentStations().Exists(t => t == m_station))
                {
                    DisplayChecklist();
                    //m_travelerManager.SubmitTraveler(m_current, m_station);
                    m_current = traveler;

                    HandleTravelersChanged();
                    return new ClientMessage();
                }
                else
                {
                    return new ClientMessage("Info", "Traveler " + traveler.ID.ToString("D6") + " is not at this station  :(");
                }
            } else
            {
                UpdateUI();
            }
            return new ClientMessage();
        }
        public ClientMessage LoadItem(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                TravelerItem item = traveler.FindItem(Convert.ToUInt16(obj["itemID"]));
                return LoadItem(item);
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error loading item");
            }
        }
        private ClientMessage LoadItem(TravelerItem item)
        {
            LoadTraveler(item.Parent);
            m_item = item;
            

            //Dictionary<string, string> returnParams = new Dictionary<string, string>()
            //{
            //    {"traveler", ExportTraveler(item.Parent)},
            //    {"item",m_item.ToString() },
            //    {"sequenceID",item.Parent.PrintSequenceID(m_item).Quotate() }
            //};

            if (m_station.Mode == StationMode.Serial) LoadTimerFor(m_item);

            HandleTravelersChanged();
            //return new ClientMessage("LoadItem", returnParams.Stringify());
            return new ClientMessage();
        }
        //public ClientMessage LoadCurrent(string json)
        //{
        //    try
        //    {
        //        if (m_current != null) {
        //            if (m_item != null) {
        //                //Dictionary<string, string> returnParams = new Dictionary<string, string>()
        //                //{
        //                //    {"traveler", ExportTraveler(m_current)},
        //                //    {"item", m_item.ToString() },
        //                //    {"sequenceID",m_current.PrintSequenceID(m_item).Quotate() }
        //                //};
        //                //return new ClientMessage("LoadItem", returnParams.Stringify());
        //            } else
        //            {
        //                return new ClientMessage("LoadTraveler", ExportTraveler(m_current));
        //            }
        //        }
        //        return new ClientMessage();
        //    }
        //    catch (Exception ex)
        //    {
        //        Server.LogException(ex);
        //        return new ClientMessage("Info", "Error loading current");
        //    }
        //}
        public ClientMessage SelectItem(TravelerItem item)
        {
            SearchedItem = item;
            if (item.PendingAt(m_station) || item.InProcessAt(m_station))
            {
                if (!item.Started(m_station))
                {
                    // ***************************************************
                    // First scan starts work and moves to InProcess queue
                    // ***************************************************

                    // add a flag event
                    item.Start(m_user, m_station);
                    // m_travelerManager.AddTravelerEvent(new ProcessEvent(m_user, m_station, 0, ProcessType.Started), traveler, item);
                    //item.History.Add();
                    // start the timer
                    //m_partTimer.Start("StartPartTimer");

                    // this is Table pack station, print Table label on search submission 

                    if (m_station == StationClass.GetStation("Table-Pack"))
                    {
                        item.Parent.PrintLabel(SearchedItem.ID, LabelType.Table);
                    }
                }
                else if (m_item != item)
                {
                    // ******************************
                    // Second scan loads the item
                    // ******************************
                    LoadItem(item);
                    if (m_station.Type == "tablePack" && !m_item.CartonPrinted)
                    {
                        m_current.PrintLabel(m_item.ID, LabelType.Pack);
                        m_item.CartonPrinted = true;
                    }
                }
                else
                {
                    // ******************************
                    // Third scan Completes the item
                    // ******************************
                    CompleteItem();
                }
            }
            else if (item.BeenCompleted(m_station))
            {
                return ReworkOptions(item, item.Parent.PrintID(item) + " has been completed at this station");
            }
            else
            {
                return ReworkOptions(item, item.Parent.PrintID(item) + " has not been completed at any of its prerequisites");
            }
            return new ClientMessage();
        }
        public ClientMessage SearchSubmitted(string json)
        {
            try
            {
                if (json.Length > 0)
                {
                    Traveler traveler = null;
                    TravelerItem item = null;
                    Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                    traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                    if (traveler != null)
                    {
                        ushort itemID;
                        if (ushort.TryParse(obj["itemID"], out itemID))
                        {
                            item = traveler.FindItem(itemID);
                            return SelectItem(item);
                        }
                        else
                        {
                            SendMessage(LoadTraveler(json).ToString());
                            return new ClientMessage("Info", traveler.ID.ToString() + "-" + obj["itemID"] + " does not exist");
                        }
                    } else
                    {
                        return new ClientMessage("Info", obj["travelerID"] + " does not exist");
                    }
                }
                else
                {
                    
                    return new ClientMessage();
                }
            } catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error processing search event");
            }
        }
        private ClientMessage ReworkOptions(TravelerItem item, string message)
        {
            if (item.PendingRework)
            {
                Dictionary<string, string> options = new Dictionary<string, string>() {
                    
                    {"Deflag Rework Status","DeflagReworkForm" },
                    {"View Details","ViewReworkDetails" },
                    {"Close","CloseAll"}
                };
                if (item.BeenCompleted(m_station))
                {
                    options.Add("Rework Now", "Rework");
                }
                return ControlPanel.Options(
                    message + "<br>This item has been flagged as reworkable, y a y!<br>What would you like to do?",
                    options
                );
            }
            else
            {
                return ControlPanel.Options(
                    message + "<br>What would you like to do?",
                    new Dictionary<string, string>()
                    {
                        {"Flag as rework","ReworkForm"},
                        {"Close","CloseAll" }
                    }
                );
            }
            // return new Cl
        }
        public ClientMessage LabelPopup(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                string returnParam = new Dictionary<string, string>()
                {
                    {"traveler",traveler.ToString() },
                    {"labelTypes",ExtensionMethods.GetNames<LabelType>().Stringify() }
                }.Stringify();
                return new ClientMessage("PrintLabelPopup", returnParam);
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error opening print dialog");
            }
        }
        public ClientMessage PrintLabel(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                int? qty = obj.ContainsKey("quantity") ? Convert.ToInt32(obj["quantity"]) : (int?)null;
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                return new ClientMessage("Info", traveler.PrintLabel(Convert.ToUInt16(obj["itemID"]), (LabelType)Enum.Parse(typeof(LabelType), obj["labelType"]), qty, true,station:m_station));

            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Could not print label(s) due to a pesky error :(");
            }
        }
        public ClientMessage OptionsMenu(string json)
        {
            try
            {
               

                Column options = new Column()
                {
                    new Button("Undo", "Undo",style: new Style("undoImg"))
                };
                // options that require a traveler
                if (m_current != null)
                {
                    // the parameter that returns with all the control events
                    string returnParam = new Dictionary<string, string>()
                    {
                        {"travelerID", m_current.ID.ToString() }
                    }.Stringify();
                    options.Add(new Button("Print Labels", "LabelPopup", returnParam));

                }
                ControlPanel panel = new ControlPanel("Options", options);
                return new ClientMessage("ControlPanel", panel.ToString());
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error opening options menu");
            }
        }
        public ClientMessage Undo(string json)
        {
            try
            {
                UserAction lastAction = m_history.Last();
                m_history.Remove(lastAction);
                if (lastAction.Method == "SubmitTraveler")
                {
                    Traveler traveler = (Traveler)lastAction.Parameters["traveler"];
                    List<TravelerItem> items = (List<TravelerItem>)lastAction.Parameters["items"];
                    foreach (TravelerItem item in items)
                    {
                        item.Undo();
                        if (traveler.GetNextStation(item.ID) == StationClass.GetStation("Finished"))
                        {
                            item.History.Remove(item.History.Last());
                        }
                    }
                    if (traveler is Table && m_station.CreatesThis(traveler))
                    {
                        if (traveler.ChildTravelers.Count > 0)
                        {
                            m_travelerManager.RemoveTraveler(traveler.ChildTravelers.Last());
                        }
                    }
                    m_travelerManager.OnTravelersChanged(new List<Traveler>() { traveler });
                }

                return CloseAll();
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Can't undo..");
            }
        }
        public ClientMessage ReworkForm(string json)
        {
            try
            {
                m_partTimer.Stop("StopPartTimer");
                JsonObject reworkReport = (JsonObject)ConfigManager.GetJSON("scrapReport");
                JsonArray vendorReasons = (JsonArray)reworkReport["vendor"];
                JsonArray productionReasons = (JsonArray)reworkReport["production"];

                Form form = new Form();
                form.Title = "Rework";
                form.Selection("source", "Source", new List<string>() { "vendor", "production" }, "production");
                form.Selection("reason", "Reason", productionReasons.ToList().Concat(vendorReasons.ToList()).ToList());
                form.Checkbox("startedWork", "Started Work", false);

                return form.Dispatch("FlagRework");
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error loading rework form");
            }
        }
        public ClientMessage DeflagReworkForm(string json)
        {
            try
            {
                return Form.CommentForm("Deflag rework","reason","Reason").Dispatch("DeflagRework", json);
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error loading rework form");
            }
        }
        public ClientMessage FlagRework(string json)
        {
            try
            {
                if (SearchedItem != null)
                {
                    SearchedItem.FlagRework(m_user, m_station, new Form(json));
                }
                return new ClientMessage();
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error reworking part");
            }
        }
        public ClientMessage DeflagRework(string json)
        {
            try
            {
                if (SearchedItem != null)
                {
                    SearchedItem.DeflagRework(m_user, m_station, new Form(json));
                }
                return new ClientMessage();
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error cancelling rework status");
            }
        }
        public ClientMessage ViewReworkDetails(string json)
        {
            try
            {
                if (SearchedItem != null)
                {
                    return PrintForm(new Form(SearchedItem.History.OfType<Documentation>().Last(e => e.LogType == LogType.FlagRework).Data));
                }
                return new ClientMessage();
            } catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error loading rework details");
            }
            
        }
        public ClientMessage Rework(string json)
        {
            try
            {
                //JsonObject obj = (JsonObject)JSON.Parse(json);
                //TravelerItem item = Server.TravelerManager.FindTraveler(obj["travelerID"]).FindItem(Convert.ToUInt16((int)obj["itemID"]));
                if (SearchedItem != null)
                {
                    // Add the rework event
                    SearchedItem.Rework(m_user, m_station);
                    // deselect item
                    if (m_item == SearchedItem)
                    {
                        m_item = null;
                        HandleTravelersChanged();
                    }

                    SendMessage(CloseAll());
                    // Re-search and take action
                    return SelectItem(SearchedItem);
                }
                return new ClientMessage();
            } catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error reworking part");
            }
        }
        public override ClientMessage Logout(string json)
        {
            m_partTimer.Clear("ClearPartTimer");
            m_stationTimer.Clear("ClearStationTimer");
            // auto-submit completed items
            //if (m_current != null) SubmitTraveler("");
            m_current = null;
            m_item = null;
            UpdateUI();
            return base.Logout(json);
        }
    }
}
