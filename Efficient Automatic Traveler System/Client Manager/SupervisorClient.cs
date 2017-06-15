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

namespace Efficient_Automatic_Traveler_System
{
    class SupervisorClient : Client, ISupervisorClient, ITravelers
    {
        #region Public Methods
        public SupervisorClient(TcpClient client, ITravelerManager travelerManager) : base(client)
        {
            AccessLevel = AccessLevel.Supervisor;
            m_travelerManager = travelerManager;
            m_travelers = m_travelerManager.GetTravelers;
            m_viewState = ItemState.PreProcess;
            m_viewType = typeof(Table);
            SendMessage((new ClientMessage("InitStations",StationClass.GetStations().Stringify())).ToString());
            SendMessage((new ClientMessage("InitLabelTypes", ExtensionMethods.Stringify<LabelType>())).ToString());
            SendMessage((new ClientMessage("InterfaceOpen")).ToString());
            HandleTravelersChanged(m_travelerManager.GetTravelers);
            KanbanManager.KanbanChanged += new KanbanChangedSubscriber(HandleKanbanChanged);
        }
        public void HandleTravelersChanged(List<Traveler> travelers)
        {
            bool mirror = true; // travelers.Count == m_travelerManager.GetTravelers.Count;
            travelers = m_travelerManager.GetTravelers;
            Dictionary<string, string> message = new Dictionary<string, string>();
            List<Traveler> filtered = new List<Traveler>(travelers);
            if (m_filterState)
            {
                filtered.RemoveAll(x => (x.State != m_viewState && !x.Items.Exists(y => y.State == m_viewState)));
            }
            if (m_filterType)
            {
                filtered.RemoveAll(x => !m_viewType.IsAssignableFrom(x.GetType()));
            }
            // the package send to the client, contains all the stations and their respective traveler queue items
            Dictionary<string, string> stations = new Dictionary<string, string>();
            List<StationClass> visibleStations = filtered.SelectMany(f => f.CurrentStations(m_viewState)).Distinct().ToList();
            foreach (StationClass station in visibleStations)
            {
                List<string> travelerStrings = new List<string>();
                foreach (Traveler traveler in filtered.Where(f => f.CurrentStations(m_viewState).Contains(station)))
                {
                    travelerStrings.Add(ExportTraveler(traveler, station));
                }
                Dictionary<string, string> stationObj = new Dictionary<string, string>()
                {
                    {"travelers", travelerStrings.Stringify(false)}
                };
                stations.Add(station.Name, stationObj.Stringify());
            }
            message.Add("stations", stations.Stringify(false));
            message.Add("mirror", mirror.ToString().ToLower());
            SendMessage(new ClientMessage("HandleTravelersChanged",message.Stringify()).ToString());
        }

        public void HandleKanbanChanged()
        {
            QueryClient("application.popupManager.Exists('Kanban Monitor')", "KanbanMonitor");
        }
        public void DisplayKanbanMonitor()
        {
            SendMessage(new ClientMessage("KanbanMonitor").ToString());
        }
        #endregion
        //----------------------------------
        #region Private Methods
        // standard export for supervisor travelers
        private string ExportTraveler(Traveler traveler, StationClass station)
        {
            Dictionary<string, string> travelerJSON = new StringStream(traveler.ToString()).ParseJSON(false);
            List<StationClass> stationsToDisplay = traveler.CurrentStations();
            //if (m_viewState == ItemState.PreProcess) stationsToDisplay.Add(StationClass.GetStation("Start"));
            Dictionary<string, string> queueItem = new Dictionary<string, string>();
            queueItem.Add("queueItem", traveler.ExportStationSummary(station));
            travelerJSON.Merge(queueItem);
            travelerJSON.Merge(traveler.ExportProperties());
            return travelerJSON.Stringify();
        }
        
        #endregion
        #region Properties
        protected ITravelerManager m_travelerManager;
        protected List<Traveler> m_travelers;
        #endregion
        //----------------------------------
        // JS client interface (these are the properties visible to the js interface calling system)
        public ClientMessage SetViewFilter(string json)
        {
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                m_viewState = (ItemState)Enum.Parse(typeof(ItemState), obj["viewState"]);
                m_viewType = typeof(Traveler).Assembly.GetType("Efficient_Automatic_Traveler_System." + obj["viewType"]);
                m_filterState = Convert.ToBoolean(obj["filterState"]);
                m_filterType = Convert.ToBoolean(obj["filterType"]);
                HandleTravelersChanged(m_travelerManager.GetTravelers);
                return new ClientMessage();
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info","Error configuring view settings");
            }
        }
        #region ISupervisor
        public string MoveTravelerStart(string json)
        {
            return m_travelerManager.MoveTravelerStart(json);
        }
        public void CloseAllPopups()
        {
            SendMessage(new ClientMessage("CloseAll").ToString());
        }
        
        //public ClientMessage LoadTraveler(string json)
        //{
        //    return m_travelerManager.LoadTraveler(json);
        //}
        //public override ClientMessage Login(string json)
        //{
        //    try
        //    {
        //        Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
        //        ClientMessage message = base.Login(json);
        //        if (message.Method == "LoginSuccess")
        //        {
        //            Dictionary<string, string> paramObj = new Dictionary<string, string>()
        //            {
        //                {"user",message.Parameters }
        //            };
        //            if (m_user.Login(obj["PWD"]))
        //            {
        //                return new ClientMessage("LoginSuccess", paramObj.Stringify());
        //            }
        //            else
        //            {
        //                return new ClientMessage("LoginPopup", ("Invalid password").Quotate());
        //            }
        //        }
        //        else
        //        {
        //            return message;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
        //        return new ClientMessage("LoginPopup", ("System error! oops...").Quotate());
        //    }
        //}
        public ClientMessage LoadTravelerJSON(string json)
        {
            return m_travelerManager.LoadTravelerJSON(json);
        }
        public ClientMessage LoadTraveler(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                if (traveler != null)
                {
                    return new ClientMessage("LoadTraveler", traveler.Export("SupervisorClient", null));
                } else
                {
                    return new ClientMessage("Info", "That traveler does not exist right now");
                }
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Could not load traveler due to a pesky error :(");
            }
        }
        public ClientMessage LoadTravelerAt(string json)
        {
            ClientMessage returnMessage = new ClientMessage();
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));


                Dictionary<string, string> exportedProps = new StringStream(ExportTraveler(traveler,StationClass.GetStation(obj["station"]))).ParseJSON(false);
                exportedProps["station"] = obj["station"].Quotate();
                if (traveler != null)
                {
                    returnMessage = new ClientMessage("LoadTravelerAt", exportedProps.Stringify());
                }
                else
                {
                    returnMessage = new ClientMessage("Info", "Invalid traveler number");
                }
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
                returnMessage = new ClientMessage("Info", "error");
            }
            return returnMessage;
            //return m_travelerManager.LoadTravelerAt(json);
        }

        public ClientMessage LoadItem(string json)
        {
            return ItemPopup(json);
            //return m_travelerManager.LoadItem(json);
        }
        // the fields that are visible in the traveler popup
        public ClientMessage TravelerPopup(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                StationClass station = StationClass.GetStation(obj["station"]);
                // the parameter that returns with all the control events
                string returnParam = new Dictionary<string, string>()
                {
                    {"travelerID", traveler.ID.ToString() }
                }.Stringify();
                //=================================
                // STYLES
                Dictionary<string, string> spaceBetween = new Dictionary<string, string>() { { "justifyContent", @"""space-between""" } };
                List<string> leftAlign = new List<string>() { "leftAlign" };
                List<string> rightAlign = new List<string>() { "rightAlign" };
                List<string> shadow = new List<string>() { "shadow" };
                List<string> white = new List<string>() { "white" };
                //=================================
                Column fields = new Column(dividers: true);
                if (traveler.Comment != "")
                {
                    fields.Add(
                        new TextNode(traveler.Comment,styleClasses: new Style("red","shadow"))
                    );
                }
                fields.Add(new Row(style: spaceBetween)
                    {
                        new TextNode("ID",styleClasses: new Style("leftAlign")), new TextNode(traveler.ID.ToString(),styleClasses: new Style("white","rightAlign","shadow"))
                    }
                );
                //fields.Add(
                //    new Row(justify: "space-between")
                //    {
                //        new TextNode("Type",textAlign: "left"), new TextNode(traveler.GetType().Name,"white","right")
                //    }
                //);
                List<string> stations = StationClass.GetStations().Where(x => x.CreatesThis(traveler)).Select( y => y.Name).ToList();
                stations.Add("Start");
                fields.Add(
                    new Row(style: spaceBetween)
                    {
                        new TextNode("Starting station",styleClasses: new Style("leftAlign")), new Selection("Station","MoveTravelerStart",stations,traveler.Station.Name,returnParam)
                    }
                );
                if (traveler is IPart)
                {
                    fields.Add(
                        new Row(style: spaceBetween)
                        {
                            new TextNode("Model",styleClasses: new Style("leftAlign")), new TextNode((traveler as IPart).ItemCode,styleClasses:new Style("white","rightAlign","shadow"))
                        }
                    );
                }
                if (traveler is Table)
                {
                    fields.Add(
                        new Row(style: spaceBetween)
                        {
                            new TextNode("Shape",styleClasses: new Style("leftAlign")), new TextNode((traveler as Table).Shape,styleClasses: new Style("white","rightAlign","shadow"))
                        }
                    );
                }
                
                fields.Add(
                    new Row(style: spaceBetween)
                    {
                        new TextNode("Qty on traveler",styleClasses: new Style("leftAlign")), new TextNode(traveler.Quantity.ToString(),styleClasses: new Style("white","rightAlign","shadow"))
                    }
                );
                
                if (traveler.ParentOrders.Count == 0)
                {
                    fields.Add(new TextNode("Make to Stock",styleClasses: new Style("red","shadow")));
                } else
                {
                    // Orders
                    Column orders = new Column(styleClasses: new Style("blackout__popup__controlPanel__list"));
                    foreach (Order order in traveler.ParentOrders)
                    {
                        orders.Add(new Button(order.SalesOrderNo,"OrderPopup",@"{""orderNo"":" + order.SalesOrderNo.Quotate() + "}"));
                    }
                    fields.Add(
                        new Row(style: spaceBetween)
                        {
                            new TextNode("Orders",styleClasses: new Style("leftAlign")), orders
                        }
                    );
                }
                // Parents
                if (traveler.ParentTravelers.Count > 0)
                {
                    Column parents = new Column(styleClasses: new Style("blackout__popup__controlPanel__list"));
                    foreach (Traveler parent in traveler.ParentTravelers)
                    {
                        parents.Add(new Button(parent.ID.ToString(), "LoadTraveler", "{\"travelerID\":" + parent.ID + "}"));
                    }
                    fields.Add(
                        new Row(style: spaceBetween)
                        {
                            new TextNode("Parents",styleClasses: new Style("leftAlign")), parents
                        }
                    );
                }
                // Children
                if (traveler.ChildTravelers.Count > 0)
                {
                    Column children = new Column(styleClasses: new Style("blackout__popup__controlPanel__list"));
                    foreach (Traveler child in traveler.ChildTravelers)
                    {
                        children.Add(new Button(child.ID.ToString(), "LoadTraveler", "{\"travelerID\":" + child.ID + "}"));
                    }
                    fields.Add(
                        new Row(style: spaceBetween)
                        {
                            new TextNode("Children",styleClasses: new Style("leftAlign")), children
                        }
                    );
                }
                if (station != null)
                {
                    fields.Add(
                        new Row(style: spaceBetween)
                        {
                            new TextNode("Station",styleClasses: new Style("leftAlign")), new TextNode(station.Name,styleClasses: new Style("white","rightAlign","shadow"))
                        }
                    );
                    fields.Add(
                        new Row(style: spaceBetween)
                        {
                            new TextNode("Pending",styleClasses: new Style("leftAlign")), new TextNode(traveler.QuantityPendingAt(station).ToString(),styleClasses: new Style("white","rightAlign","shadow"))
                        }
                    );
                    if (traveler.QuantityCompleteAt(station) > 0)
                    {
                        fields.Add(
                            new Row(style: spaceBetween)
                            {
                                new TextNode("Complete",styleClasses: new Style("leftAlign")), new TextNode(traveler.QuantityCompleteAt(station).ToString(),styleClasses: new Style("white","rightAlign","shadow"))
                            }
                        );
                    }
                }
                if (traveler.Items.Count > 0)
                {
                    Column items = new Column(styleClasses: new Style("blackout__popup__controlPanel__list"));
                    foreach (TravelerItem item in traveler.Items)
                    {
                        items.Add(new Button(traveler.PrintSequenceID(item), "ItemPopup", "{\"travelerID\":" + traveler.ID + ",\"itemID\":" + item.ID + "}"));
                    }
                    fields.Add(
                        new Row(style: spaceBetween)
                        {
                            new TextNode("Items",styleClasses: new Style("leftAlign")), items
                        }
                    );
                }

                Column controls = new Column()
                {
                    new Button("More Info","LoadTravelerJSON",returnParam),
                    new Button("Disintegrate","DisintegrateTraveler",returnParam),
                    new Button("Enter Production","EnterProduction",returnParam)
                };
                
                ControlPanel panel = new ControlPanel(traveler.GetType().Name, new Row() { fields, controls });
                return new ClientMessage("ControlPanel", panel.ToString());
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error when getting display fields");
            }
        }
        // the order popup
        public ClientMessage OrderPopup(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Order order = Server.OrderManager.FindOrder(obj["orderNo"]);
                if (order != null)
                {
                    Column orderPopup = new Column(true);
                    orderPopup.Add(new Row(styleClasses:new Style("justify-space-between"))
                        {
                            new TextNode("Ship Date",styleClasses: new Style("leftAlign")), new TextNode(order.ShipDate.ToString("MM/dd/yyyy"),styleClasses: new Style("white","rightAlign","shadow"))
                        }
                    );
                    orderPopup.Add(new Row(styleClasses: new Style("justify-space-between"))
                        {
                            new TextNode("Order Date",styleClasses: new Style("leftAlign")), new TextNode(order.OrderDate.ToString("MM/dd/yyyy"),styleClasses: new Style("white","rightAlign","shadow"))
                        }
                    );
                    orderPopup.Add(new Row(styleClasses: new Style("justify-space-between"))
                        {
                            new TextNode("Customer",styleClasses: new Style("leftAlign")), new TextNode(order.CustomerNo,styleClasses: new Style("white","rightAlign","shadow"))
                        }
                    );
                    orderPopup.Add(new Row(styleClasses: new Style("justify-space-between"))
                        {
                            new TextNode("Status",styleClasses: new Style("leftAlign")), new TextNode(order.Status.ToString(),styleClasses: new Style("white","rightAlign","shadow"))
                        }
                    );
                    NodeList lineItems = new NodeList(DOMtype: "table");

                    // Header
                    NodeList header = new NodeList( DOMtype: "tr");
                    header.Add(new TextNode("Item Code", styleClasses: new Style("mediumBorder"), DOMtype: "th"));
                    header.Add(new TextNode("Ordered", styleClasses: new Style("mediumBorder"), DOMtype: "th"));
                    header.Add(new TextNode("On Hand", styleClasses: new Style("mediumBorder"), DOMtype: "th"));
                    header.Add(new TextNode("Traveler", styleClasses: new Style("mediumBorder"), DOMtype: "th"));
                    header.Add(new TextNode("Shipped", styleClasses: new Style("mediumBorder"), DOMtype: "th"));
                    lineItems.Add(header);
                    foreach (OrderItem item in order.Items)
                    {
                        // Detail
                        NodeList row = new NodeList(DOMtype: "tr");
                        row.Add(new TextNode(item.ItemCode, styleClasses: new Style("mediumBorder"), DOMtype: "td"));
                        row.Add(new TextNode(item.QtyOrdered.ToString(), styleClasses: new Style("mediumBorder"), DOMtype: "td"));
                        row.Add(new TextNode(item.QtyOnHand.ToString(), styleClasses: new Style("mediumBorder"), DOMtype: "td"));
                        if (item.ChildTraveler >= 0)
                        {
                            row.Add(new Button(item.ChildTraveler.ToString("D6"), "LoadTraveler", @"{""travelerID"":" + item.ChildTraveler + "}"));
                        } else
                        {
                            row.Add(new Node(styleClasses: new Style("mediumBorder"), DOMtype: "td")); // blank if no child traveler
                        }
                        row.Add(new TextNode(item.QtyShipped.ToString(), styleClasses: new Style("mediumBorder"), DOMtype: "td"));
                        lineItems.Add(row);
                    }
                    orderPopup.Add(lineItems);
                    return new ClientMessage("ControlPanel", new ControlPanel("Order " + order.SalesOrderNo, orderPopup).ToString());
                }
                else
                {
                    return new ClientMessage("Info", "Order " + obj["orderNo"] + " could not be found in EATS");
                }
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error loading order popup");
            }
        }
        // the item popup
        public ClientMessage ItemPopup(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                TravelerItem item = traveler.FindItem(Convert.ToUInt16(obj["itemID"]));

                Column fields = new Column(true);
                fields.Add(
                    new Row(styleClasses: new Style("justify-space-between"))
                    {
                        new TextNode("Traveler",styleClasses: new Style("leftAlign")), new Button(traveler.ID.ToString(),"LoadTraveler",json)
                    }
                );
                fields.Add(
                    new Row(styleClasses: new Style("justify-space-between"))
                    {
                        new TextNode("Station",styleClasses: new Style("leftAlign")), new Selection("Station","MoveItem",StationClass.GetStations().Select(s => s.Name).ToList(),item.Station.Name,json)
                    }
                );
                fields.Add(
                    new Row(styleClasses: new Style("justify-space-between"))
                    {
                        new TextNode("ItemCode",styleClasses: new Style("leftAlign")), new TextNode(item.ItemCode,styleClasses: new Style("white","rightAlign","shadow"))
                    }
                );
                fields.Add(
                    new Row(styleClasses: new Style("justify-space-between"))
                    {
                        new TextNode("State",styleClasses: new Style("leftAlign")), new TextNode(item.State.ToString(),styleClasses: new Style("white","rightAlign","shadow"))
                    }
                );
                if (item.History.Count > 0)
                {
                    Column history = new Column(styleClasses: new Style("blackout__popup__controlPanel__list"));
                    int index = 0;
                    foreach (Event evt in item.History)
                    {
                        history.Add(new Button(evt.Date.ToString("MM/dd/yyyy") + " " + (evt is ScrapEvent ? "Scrapped" : evt is ProcessEvent ? ((ProcessEvent)evt).Process.ToString(): evt is LogEvent ? ((LogEvent)evt).LogType.ToString() : "Event"), "EventPopup", "{\"travelerID\":" + traveler.ID + ",\"itemID\":" + item.ID + ",\"eventIndex\":" + index + "}"));
                        index++;
                    }
                    fields.Add(
                        new Row(styleClasses: new Style("justify-space-between"))
                        {
                            new TextNode("History",styleClasses: new Style("leftAlign")), history
                        }
                    );
                }
                Column controls = new Column()
                {
                    new Button("Print Labels","LabelPopup",json)
                };
                
                return new ClientMessage("ControlPanel", new ControlPanel(traveler.PrintSequenceID(item), new Row() { fields, controls }).ToString());
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error loading item popup");
            }
        }
        public string MoveItem(string json)
        {
            ClientMessage returnMessage = new ClientMessage();
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                StationClass station = StationClass.GetStation(obj["value"]);
                List<string> travelerIDs = new List<string>();
                if (obj.ContainsKey("travelerIDs")) travelerIDs = new StringStream(obj["travelerIDs"]).ParseJSONarray();
                if (obj.ContainsKey("travelerID")) travelerIDs.Add(obj["travelerID"]);
                foreach (string ID in travelerIDs)
                {
                    Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(ID));
                    if (traveler != null && station != null)
                    {
                        TravelerItem item = traveler.FindItem(Convert.ToUInt16(obj["itemID"]));
                        if (item != null)
                        {
                            item.Station = station;
                        }
                    }
                }
                m_travelerManager.OnTravelersChanged(m_travelerManager.GetTravelers);
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
                returnMessage = new ClientMessage("Info", "error");
            }
            return returnMessage.ToString();
        }
        public ClientMessage EventPopup(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                TravelerItem item = traveler.FindItem(Convert.ToUInt16(obj["itemID"]));
                Event evt = item.History[Convert.ToInt32(obj["eventIndex"])];

                Column fields = new Column(true);
                fields.Add(
                    new Row(styleClasses: new Style("justify-space-between"))
                    {
                        new TextNode("Item",styleClasses: new Style("leftAlign")), new Button(traveler.PrintSequenceID(item),"ItemPopup",json)
                    }
                );
                fields.Add(
                    new Row(styleClasses: new Style("justify-space-between"))
                    {
                        new TextNode("Date",styleClasses: new Style("leftAlign")), new TextNode(evt.Date.ToString("MM/dd/yyyy @ hh:mm tt"),styleClasses: new Style("white","rightAlign","shadow"))
                    }
                );
                if (evt is ProcessEvent) {
                    ProcessEvent process = (ProcessEvent)evt;
                    fields.Add(
                        new Row(styleClasses: new Style("justify-space-between"))
                        {
                        new TextNode("Station",styleClasses: new Style("leftAlign")), new TextNode(process.Station.Name,styleClasses: new Style("white","rightAlign","shadow"))
                        }
                    );
                    fields.Add(
                        new Row(styleClasses: new Style("justify-space-between"))
                        {
                        new TextNode("Process",styleClasses: new Style("leftAlign")), new TextNode(process.Process.ToString(),styleClasses: new Style("white","rightAlign","shadow"))
                        }
                    );
                    fields.Add(
                        new Row(styleClasses: new Style("justify-space-between"))
                        {
                        new TextNode("Duration",styleClasses: new Style("leftAlign")), new TextNode(Math.Round(process.Duration,2).ToString() + " min",styleClasses: new Style("white","rightAlign","shadow"))
                        }
                    );
                    fields.Add(
                        new Row(styleClasses: new Style("justify-space-between"))
                        {
                        new TextNode("User",styleClasses: new Style("leftAlign")), new TextNode(process.User.Name,styleClasses: new Style("white","rightAlign","shadow"))
                        }
                    );
                } else
                {
                    LogEvent log = (LogEvent)evt;
                    fields.Add(
                        new Row(styleClasses: new Style("justify-space-between"))
                        {
                        new TextNode("Station",styleClasses: new Style("leftAlign")), new TextNode(log.Station.Name,styleClasses: new Style("white","rightAlign","shadow"))
                        }
                    );
                    fields.Add(
                        new Row(styleClasses: new Style("justify-space-between"))
                        {
                        new TextNode("Log type",styleClasses: new Style("leftAlign")), new TextNode(log.LogType.ToString(),styleClasses: new Style("white","rightAlign","shadow"))
                        }
                    );
                }
                return new ClientMessage("ControlPanel", new ControlPanel(traveler.PrintSequenceID(item) + " Event", fields).ToString());
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error loading item popup");
            }
        }
        public ClientMessage MultiTravelerOptions(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                List<string> selectedIDs = new StringStream(obj["travelerIDs"]).ParseJSONarray();
                
                //foreach (string selectedID in selectedIDs)
                //{
                //    Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(selectedID));
                    
                //}

                // the parameter that returns with all the control events
                string returnParam = new Dictionary<string, string>()
                {
                    {"travelerIDs", obj["travelerIDs"] }
                }.Stringify();
                Dictionary<string, string> flexStart = new Dictionary<string, string>() { { "justifyContent", @"""flex-start""" } };
                Column IDs = new Column(style: flexStart);
                foreach (string selectedID in selectedIDs)
                {
                    IDs.Add(new TextNode(selectedID));
                }
                Column controls = new Column()
                {
                    new Button("Disintegrate","DisintegrateTraveler",returnParam),
                    new Button("Enter Production","EnterProduction",returnParam),
                    new TextNode("Starting Station"),
                    new Selection("Starting Station","MoveTravelerStart",StationClass.StationNames(),returnParam: returnParam)
                };

                ControlPanel panel = new ControlPanel("Travelers", new Row() { IDs, controls });
                return new ClientMessage("ControlPanel", panel.ToString());
            } catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error getting multi-traveler options");
            }
        }
        public ClientMessage OptionsMenu(string json)
        {
            try
            {
                Dictionary<string, string> flexStart = new Dictionary<string, string>() { { "justifyContent", @"""flex-start""" } };
                Column download = new Column(style: flexStart)
                {
                    new TextNode("Download"),
                    new Button("Pre-Process Tables","DownloadSummary",@"{""sort"":""PreProcess"",""type"":""Table""}"),
                    new Button("Production Report", "ExportProduction",@"{""sort"":""All"",""type"":""Table""}"),
                    new Button("Scrap Report", "ExportScrap",@"{""sort"":""All"",""type"":""Table""}"),
                    new Button("User Report", "DateRangePopup",@"{""innerCallback"":""DownloadUserSummary""}"),
                    new Button("Rework Report", "ExportRework",@"{""sort"":""All"",""type"":""Table""}")
                };
                Column manage = new Column(style: flexStart)
                {
                    new TextNode("Manage"),
                    new Button("New User","UserForm"),
                    new Button("Edit User","SearchPopup",@"{""interfaceCall"":""EditUserForm"",""message"":""Search for a user by name or ID""}"),
                    new Button("New Traveler","TravelerForm"),
                    new Button("Kanban Monitor", "KanbanMonitor")
                };
                Column view = new Column(style: flexStart)
                {
                    new TextNode("View"),
                    new Button("View Summary","CreateSummary",@"{""sort"":""Active"",""type"":""Table"",""from"":"""",""to"":""""}"),
                    new Button("View Orders","OrderListPopup")
                };
                ControlPanel panel = new ControlPanel("Options", new Row() { download , manage, view});
                return new ClientMessage("ControlPanel", panel.ToString());
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error when getting display fields");
            }
        }
        public ClientMessage SearchPopup(string json)
        {
            try
            {
                return new ClientMessage("SearchPopup", json);
            } catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error reflecting function call");
            }
        }
        public ClientMessage Test(string json)
        {
            return new ClientMessage("Info", json);
        }
        public ClientMessage CreateSummary(string json)
        {
            return m_travelerManager.CreateSummary(json);
        }

        public ClientMessage DisintegrateTraveler(string json)
        {
            return m_travelerManager.DisintegrateTraveler(json);
        }

        public ClientMessage EnterProduction(string json)
        {
            return m_travelerManager.EnterProduction(json);
        }

        public ClientMessage DownloadSummary(string json)
        {
            return m_travelerManager.DownloadSummary(json);
        }
        public ClientMessage DownloadUserSummary(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                DateTime A = DateTime.Parse(obj["A"]);
                DateTime B = DateTime.Parse(obj["B"]);
                return new ClientMessage("Redirect", new Summary(A,B).UserCSV().Quotate());
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error generating User summary");
            }
        }
        public ClientMessage DateRangePopup(string json)
        {
            try
            {
                return new ClientMessage("DateRangePopup", json);
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error creating date range popup");
            }
        }
        public ClientMessage OrderListPopup(string json)
        {
            try
            {
                Column list = new Column(true,styleClasses: new Style("scrollY"));
                foreach (Order order in Server.OrderManager.GetOrders.Where( o => o.Items.Exists( i => i.ChildTraveler >= 0)))
                {
                    list.Add(new Button(order.SalesOrderNo, "OrderPopup", @"{""orderNo"":" + order.SalesOrderNo.Quotate() + "}"));
                }
                return new ClientMessage("ControlPanel",new ControlPanel("Orders",list).ToString());
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error creating order list popup");
            }
        }
        public ClientMessage LabelPopup(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                //Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                Form form = new Form();
                form.Name = "Print Labels";
                form.Selection("labelType", "Label Type", ExtensionMethods.GetNames<LabelType>());
                form.Integer("quantity", "Quantity", 1, 100, 1);
                form.Selection("printer", "Printer", new StringStream(ConfigManager.Get("printers")).ParseJSONarray());
                return form.Dispatch("PrintLabel", json);
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
                Form form = new Form(obj["form"]);
                Dictionary<string, string> parameters = new StringStream(obj["parameters"]).ParseJSON();
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(parameters["travelerID"]));
                int qty = Convert.ToInt32(form.ValueOf("quantity"));
                return new ClientMessage("Info",traveler.PrintLabel(Convert.ToUInt16(parameters["itemID"]), (LabelType)Enum.Parse(typeof(LabelType), form.ValueOf("labelType")),qty > 0 ? qty : 1,true));
                
            } catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Could not print label(s) due to a pesky error :(");
            }
        }
        public ClientMessage ExportProduction(string json)
        {
            ClientMessage returnMessage = new ClientMessage();
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                Summary summary = new Summary(m_travelerManager as ITravelerManager, obj["type"], (SummarySort)Enum.Parse(typeof(SummarySort), obj["sort"]));
                string downloadLocation = summary.ProductionCSV();
                returnMessage = new ClientMessage("Redirect", downloadLocation.Quotate());
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
                returnMessage = new ClientMessage("Info", "error");
            }
            return returnMessage;
        }
        public ClientMessage ExportScrap(string json)
        {
            //ClientMessage returnMessage = new ClientMessage();
            //try
            //{
            //    Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
            //    Summary summary = new Summary(m_travelerManager as ITravelerManager, obj["type"], (SummarySort)Enum.Parse(typeof(SummarySort), obj["sort"]));
            //    string downloadLocation = summary.CSV("test.csv", new List<SummaryColumn>() {
            //        new SummaryColumn("ID","ID"),
            //        new SummaryColumn("ItemCode","ItemCode")
            //    });
            //    returnMessage = new ClientMessage("Redirect", downloadLocation.Quotate());
            //}
            //catch (Exception ex)
            //{
            //    Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
            //    returnMessage = new ClientMessage("Info", "error");
            //}
            //return returnMessage;
            ClientMessage returnMessage = new ClientMessage();
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                Summary summary = new Summary(m_travelerManager as ITravelerManager, obj["type"], (SummarySort)Enum.Parse(typeof(SummarySort), obj["sort"]));
                string downloadLocation = summary.ScrapCSV();
                returnMessage = new ClientMessage("Redirect", downloadLocation.Quotate());
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
                returnMessage = new ClientMessage("Info", "error");
            }
            return returnMessage;
        }
        public ClientMessage ExportTest(string json)
        {
            ClientMessage returnMessage = new ClientMessage();
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                Summary summary = new Summary(m_travelerManager as ITravelerManager, obj["type"], (SummarySort)Enum.Parse(typeof(SummarySort), obj["sort"]));
                string downloadLocation = summary.CSV("EATS Client\\test.csv", new List<SummaryColumn>() {
                    new SummaryColumn("ID","ID"),
                    new SummaryColumn("Scrapped","Scrapped")
                });
                returnMessage = new ClientMessage("Redirect", downloadLocation.Quotate());
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
                returnMessage = new ClientMessage("Info", "error");
            }
            return returnMessage;
        }
        public ClientMessage ExportRework(string json)
        {
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                Summary summary = new Summary(m_travelerManager as ITravelerManager, obj["type"], (SummarySort)Enum.Parse(typeof(SummarySort), obj["sort"]));
                string downloadLocation = summary.ReworkCSV();
                return new ClientMessage("Redirect", downloadLocation.Quotate());
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
                return new ClientMessage("Info", "Error exporting rework report");
            }
        }
        public ClientMessage QuantityAt(string json)
        {
            ClientMessage returnMessage = new ClientMessage();
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                StationClass station = StationClass.GetStation(obj["station"]);
                
                Type type = typeof(Traveler).Assembly.GetType("Efficient_Automatic_Traveler_System." + obj["type"]);
                int quantity = m_travelerManager.GetTravelers.Where(y => y.GetType() == type).Sum(x => x.QuantityAt(station));
                Dictionary<string, string> returnObj = new Dictionary<string, string>()
                {
                    {"station",station.Name.Quotate() },
                    {"quantity",quantity.ToString() }
                };
                returnMessage = new ClientMessage("QuantityAt", returnObj.Stringify());

            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
                returnMessage = new ClientMessage("Info", "error");
            }
            return returnMessage;
        }

        public ClientMessage UserForm(string json)
        {
            return new ClientMessage("UserForm", m_user.CreateForm().ToString());
        }
        public ClientMessage NewUser(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Form form = new Form(json);
                User newUser = new User(form);
                User existingUser = Server.UserManager.Find(newUser.UID);
                if (existingUser == null)
                {
                    // This is a brand spanking new user!
                    Server.UserManager.AddUser(newUser);
                    return new ClientMessage("Info", newUser.Name + " has been added!");
                } else
                {
                    // User already exists, inform the supervisor
                    return new ClientMessage("Info", "Existing user already has the ID: " + existingUser.UID);
                }
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "error");
            }
        }
        public ClientMessage EditUser(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Form form = new Form(json);
                User newUser = new User(form);
                User existingUser = Server.UserManager.Find(newUser.UID);
                if (existingUser != null)
                {
                    // User exists, lets try to update some information
                    existingUser.Update(form);
                    return new ClientMessage("Info", newUser.Name + " has been updated!");
                } else
                {
                    // User ID was changed, inform the supervisor
                    return new ClientMessage("Info", "You cannot change the user's ID");
                }
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "error");
            }
        }
        public ClientMessage EditUserForm(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                User user = Server.UserManager.Find(obj["searchPhrase"]);
                if (user != null)
                {
                    return new ClientMessage("EditUserForm",user.CreateFilledForm().ToString());
                } else
                {
                    return new ClientMessage("Info", "Could not find the requested user");
                }
            } catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "error");
            }
        }
        public ClientMessage SearchSubmitted(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                string[] parts = obj["searchPhrase"].Split('-');
                Traveler traveler = null;
                if (parts.Length > 1)
                {
                    int travelerID = Convert.ToInt32(parts[0]);
                    ushort itemID = Convert.ToUInt16(parts[1]);
                    traveler = m_travelerManager.FindTraveler(travelerID);
                    if (traveler != null)
                    {
                        TravelerItem item = traveler.FindItem(itemID);
                        if (item != null)
                        {
                            return LoadItem(@"{""travelerID"":" + traveler.ID + @",""itemID"":" + itemID + "}");
                        } else
                        {
                            SendMessage(LoadTraveler(@"{""travelerID"":" + traveler.ID + "}").ToString());
                            return new ClientMessage("Info", "Traveler " + travelerID.ToString("D6") + " has no item with ID: " + itemID);
                        }
                    } else
                    {
                        return new ClientMessage("Info", "Traveler " + travelerID.ToString("D6") + " could not be found");
                    }
                } else if (parts[0].Length <= 6) {
                    traveler = m_travelerManager.FindTraveler(Convert.ToInt32(parts[0]));
                    if (traveler != null)
                    {
                        return LoadTraveler(@"{""travelerID"":" + traveler.ID + "}");
                    }
                    else
                    {
                        return new ClientMessage("Info", "Traveler " + parts[0] + " could not be found");
                    }
                } else if (parts[0].Length == 7)
                {
                    // all orders have 7 character order numbers
                    return OrderPopup(@"{""orderNo"":" + parts[0].Quotate() + "}");
                } else
                {
                    return new ClientMessage("Info", "Incorrect format, please search:<br>a traveler, ex: 123456<br>a traveler item, ex: 123456-1234<br>an order, ex: 1234567");
                }

            } catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error occured during search event");
            }
        }
        public ClientMessage TravelerForm(string json)
        {
            return m_travelerManager.TravelerForm(json);
        }
        public async Task<ClientMessage> NewTraveler(string json)
        {
            return await m_travelerManager.NewTraveler(json);
        }
        public ClientMessage KanbanMonitor(string json)
        {
            try
            {
                return new ClientMessage("ControlPanel", KanbanManager.CreateKanbanMonitor().ToString());
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error opening Kanban monitor");
            }
        }
        // Kanban Forms
        public ClientMessage NewKanbanItemForm(string json)
        {
            return KanbanManager.NewKanbanItemForm(json);
        }
        public ClientMessage EditKanbanItemForm(string json)
        {
            return KanbanManager.EditKanbanItemForm(json);
        }
        // Kanban Form actions
        public async Task<ClientMessage> NewKanbanItem(string json)
        {
            return await KanbanManager.NewKanbanItem(json);
        }
        public async Task<ClientMessage> EditKanbanItem(string json)
        {
            return await KanbanManager.EditKanbanItem(json);
        }
        public ClientMessage DeleteKanbanItem(string json)
        {
            return KanbanManager.DeleteKanbanItem(json);
        }

        #endregion
        //-----------------------------------
        #region Properties
        private ItemState m_viewState;
        private Type m_viewType;
        private bool m_filterState;
        private bool m_filterType;
        #endregion
        //----------
        // Events
        //----------
        public event TravelersChangedSubscriber TravelersChanged;
    }
}
