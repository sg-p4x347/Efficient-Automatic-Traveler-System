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

using System.Data;

namespace Efficient_Automatic_Traveler_System
{
    class SupervisorClient : Client, ITravelers
    {
        #region Public Methods
        public SupervisorClient(TcpClient client, ITravelerManager travelerManager) : base(client)
        {
            AccessLevel = AccessLevel.Supervisor;
            m_travelerManager = travelerManager;
            ViewFilter = new Form();
            m_selected = new List<Traveler>();
            SendMessage((new ClientMessage("InitStations", StationClass.GetStations().Stringify())).ToString());
            SendMessage((new ClientMessage("InitLabelTypes", ExtensionMethods.Stringify<LabelType>())).ToString());
            SendMessage((new ClientMessage("InterfaceOpen")).ToString());
            HandleTravelersChanged();
            KanbanManager.KanbanChanged += new KanbanChangedSubscriber(HandleKanbanChanged);
            SetFilterForm();
        }
        public override void HandleTravelersChanged(bool changed = false)
        {
            try
            {
                NodeList queueArray = new NodeList(new Style("queueArray"));
                queueArray.ID = "queueArray";
                foreach (StationClass station in StationClass.GetStations())
                {
                    GlobalItemState GlobalState;
                    if (Enum.TryParse(ViewFilter.ValueOf("globalState"), out GlobalState))
                    {
                        queueArray.Add(CreateStation(station, GlobalState));
                    }
                }
                SendMessage(new ControlPanel("queueArray", queueArray, "body").Dispatch().ToString());
                //if (m_current != null) SendMessage(TravelerPopup(m_current));
            } catch (Exception ex)
            {

            }
        }
        private void SetFilterForm()
        {
            Form form = new Form();
            form.ID = "filterForm";
            // global state
            form.Radio("globalState", "Global State", ExtensionMethods.GetNames<GlobalItemState>(), GlobalItemState.PreProcess.ToString());
            // type
            form.Radio("type", "Type", new List<string>() { "Table", "Chair", "TableBox" },"Table");
            // filterType
            form.Checkbox("filterType", "Filter Type", true);
            
            SendMessage(form.Dispatch("SetViewFilter"));
        }
        public Node CreateStation(StationClass station, GlobalItemState state)
        {
            List<Traveler> travelers = VisibleTravelers(station);
            NodeList queueContainer = new NodeList(new Style("queueContainer"));
            if (!travelers.Any()) queueContainer.Style.AddStyle("display", "none");
            TextNode heading = new TextNode(station.Name, new Style("heading"));
            queueContainer.Add(heading);
            if (state == GlobalItemState.PreProcess)
            {
                queueContainer.Add(ControlPanel.CreateDictionary(new Dictionary<string, Node>()
                {
                    {"Qty Pending:",new TextNode(travelers.Sum(t => t.Quantity).ToString(),new Style("beige") )},
                    {"Total Pending Labor:",new TextNode(travelers.Sum(t => t.GetTotalLabor()).ToString() + " min",new Style("beige") )},
                }));
            }
            else if (state == GlobalItemState.InProcess)
            {
                queueContainer.Add(ControlPanel.CreateDictionary(new Dictionary<string, Node>()
                {
                    {"Qty at this station:",new TextNode(travelers.Sum(t => t.QuantityAt(station)).ToString(),new Style("beige") )}
                }));
            } else if (state == GlobalItemState.Finished)
            {
                queueContainer.Add(ControlPanel.CreateDictionary(new Dictionary<string, Node>()
                {
                    {"Qty at this station:",new TextNode(travelers.Sum(t => t.QuantityAt(station)).ToString(),new Style("beige") )}
                }));
            }
            queueContainer.Add(new Checkbox("Select All", "SelectAll", new JsonObject() { { "station", station.Name } }, VisibleTravelers(station).All(t => m_selected.Contains(t))));
            NodeList queue = CreateTravelerQueue(travelers, station);
            queue.ID = station.Name;
            queue.Style += new Style("queue");
            queueContainer.Add(queue);
            return queueContainer;
        }
        protected override Row CreateTravelerQueueItem(GlobalItemState state, Traveler traveler)
        {
            Row queueItem = base.CreateTravelerQueueItem(state, traveler);
            // checkbox
            bool selected = m_selected.Contains(traveler);
            if (this is SupervisorClient) queueItem.Add(new Checkbox("", "SelectChanged", new JsonObject() { { "travelerID", traveler.ID } }, selected, new Style("topLeft")));
            if (selected) queueItem.Style += new Style("selected");
            return queueItem;
        }
        public List<Traveler> VisibleTravelers(StationClass station)
        {
            List<Traveler> travelers = new List<Traveler>();
            GlobalItemState GlobalState;
            bool filterType;
            Type type = typeof(Traveler).Assembly.GetType("Efficient_Automatic_Traveler_System." + ViewFilter.ValueOf("type"));
            if (Enum.TryParse(ViewFilter.ValueOf("globalState"), out GlobalState)
                && Boolean.TryParse(ViewFilter.ValueOf("filterType"), out filterType)
            )
            {
                foreach (Traveler traveler in m_travelerManager.GetTravelers)
                {
                    if (!filterType || traveler.GetType() == type)
                    {
                        if (GlobalState == GlobalItemState.PreProcess)
                        {
                            if (traveler.State == GlobalItemState.PreProcess && traveler.Station == station) travelers.Add(traveler);
                        }
                        else if (GlobalState == GlobalItemState.InProcess)
                        {
                            if (traveler.Items.Any(i => i.Station == station && i.GlobalState == GlobalItemState.InProcess)
                                || (station == traveler.Station && traveler.QuantityPendingAt(station) > 0)
                            ) travelers.Add(traveler);
                            //if (traveler.State == GlobalItemState.InProcess && traveler.Items.Exists(i => i.GlobalState == m_viewState && i.Station == station) || (station == traveler.Station && traveler.QuantityPendingAt(station) > 0)) travelers.Add(traveler);
                        }
                        else
                        {
                            if (traveler.Items.Exists(i => i.GlobalState == GlobalState && i.Station == station)) travelers.Add(traveler);
                        }
                    }
                }
            }
            return travelers;
        }
        public ClientMessage SelectChanged(string json)
        {
            try
            {
                JSON obj = JSON.Parse(json);
                Traveler traveler = Server.TravelerManager.FindTraveler(obj["travelerID"]);
                if (obj["value"])
                {
                    if (!m_selected.Contains(traveler)) m_selected.Add(traveler);
                }
                else
                {
                    m_selected.Remove(traveler);
                }
                m_current = null;
                HandleTravelersChanged();
                return new ClientMessage();
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage();
            }
        }
        public ClientMessage SelectAll(string json)
        {
            try
            {
                JSON obj = JSON.Parse(json);
                StationClass station = StationClass.GetStation(obj["station"]);
                if (obj["value"])
                {
                    // select all
                    m_selected.AddRange(VisibleTravelers(station));
                }
                else
                {
                    // deselect all
                    m_selected.RemoveAll(t => VisibleTravelers(station).Contains(t));
                }
                m_current = null;
                Server.TravelerManager.OnTravelersChanged();
                return new ClientMessage();
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "error selecting all");
            }
        }
        public void HandleKanbanChanged()
        {
            QueryClient("application.popupManager.Exists('Kanban Monitor')", "KanbanMonitor");
        }
        public void DisplayKanbanMonitor()
        {
            SendMessage(new ClientMessage("KanbanMonitor").ToString());
        }
        public ClientMessage LegacyTravelerPopup(int travelerID)
        {
            return (ControlPanel.YesOrNo("Would you like to search EATS history for this traveler?",
                "LookupLegacyTraveler", returnParam: new JsonObject() { { "travelerID", travelerID } }));
        }
        public ClientMessage LookupLegacyTraveler(string json)
        {
            JsonObject obj = (JsonObject)JSON.Parse(json);
            Traveler traveler;
            ClientMessage message;
            if (Server.TravelerManager.FindLegacyTraveler(obj["travelerID"],out traveler, out message))
            {
                SelectTraveler(traveler);
                SendMessage(TravelerPopup(SelectedTraveler));
                return new ClientMessage();
            } else
            {
                return message;
            }
        }
        #endregion
        //----------------------------------
        #region Private Methods
        // standard export for supervisor travelers
        //private string ExportTraveler(Traveler traveler, StationClass station)
        //{
        //    Dictionary<string, string> travelerJSON = new StringStream(traveler.ToString()).ParseJSON(false);
        //    List<StationClass> stationsToDisplay = traveler.CurrentStations();
        //    //if (m_viewState == ItemState.PreProcess) stationsToDisplay.Add(StationClass.GetStation("Start"));
        //    Dictionary<string, string> queueItem = new Dictionary<string, string>();
        //    queueItem.Add("queueItem", traveler.ExportStationSummary(station));
        //    travelerJSON.Merge(queueItem);
        //    travelerJSON.Merge(traveler.ExportProperties());
        //    return travelerJSON.Stringify();
        //}

        #endregion
        #region Properties
        protected ITravelerManager m_travelerManager;
        #endregion
        //----------------------------------
        // JS client interface (these are the properties visible to the js interface calling system)
        public ClientMessage SetViewFilter(string json)
        {
            try
            {
                ViewFilter = new Form(json);

                //m_viewState 
                //m_viewState = (GlobalItemState)Enum.Parse(typeof(GlobalItemState), obj["viewState"]);
                //m_viewType = typeof(Traveler).Assembly.GetType("Efficient_Automatic_Traveler_System." + obj["viewType"]);
                //m_filterState = Convert.ToBoolean(obj["filterState"]);
                ////m_filterLocalState = Convert.ToBoolean(obj["filterLocalState"]);
                //m_filterType = Convert.ToBoolean(obj["filterType"]);
                HandleTravelersChanged();
                return new ClientMessage();
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error configuring view settings");
            }
        }
        #region ISupervisor
        public ClientMessage MoveTravelerStart(string json)
        {
            JSON obj = JSON.Parse(json);
            if (m_current != null) m_selected.Add(m_current);
            foreach (Traveler traveler in m_selected)
            {
                if (traveler != null && StationClass.GetStation(obj["value"]) != null)
                {
                    traveler.Station = StationClass.GetStation(obj["value"]);
                }
            }
            m_selected.Clear();
            m_current = null;
            CloseAllPopups();
            Server.TravelerManager.OnTravelersChanged();
            return new ClientMessage();
        }
        public void CloseAllPopups()
        {
            SendMessage(new ClientMessage("CloseAll").ToString());
            m_current = null;
        }

        //public ClientMessage LoadTraveler(string json)
        //{
        //    return m_currentManager.LoadTraveler(json);
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
            return LoadTraveler(m_travelerManager.FindTraveler(JSON.Parse(json)["travelerID"]));
        }
        public ClientMessage LoadTraveler(Traveler traveler)
        {
            try
            {
                if (m_selected.Count > 1)
                {
                    m_current = null;
                    MultiTravelerOptions();
                    return new ClientMessage();
                }
                else
                {
                    m_current = traveler;
                    if (m_current != null)
                    {
                        return TravelerPopup(m_current);
                    }
                    else
                    {
                        return new ClientMessage("Info", "That traveler does not exist right now");
                    }
                }
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Could not load traveler due to a pesky error :(");
            }
        }
        //public ClientMessage LoadTravelerAt(string json)
        //{
        //    ClientMessage returnMessage = new ClientMessage();
        //    try
        //    {
        //        Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
        //        Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));


        //        Dictionary<string, string> exportedProps = new StringStream(ExportTraveler(traveler,StationClass.GetStation(obj["station"]))).ParseJSON(false);
        //        exportedProps["station"] = obj["station"].Quotate();
        //        if (traveler != null)
        //        {
        //            returnMessage = new ClientMessage("LoadTravelerAt", exportedProps.Stringify());
        //        }
        //        else
        //        {
        //            returnMessage = new ClientMessage("Info", "Invalid traveler number");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
        //        returnMessage = new ClientMessage("Info", "error");
        //    }
        //    return returnMessage;
        //    //return m_currentManager.LoadTravelerAt(json);
        //}

        public ClientMessage LoadItem(string json)
        {
            return ItemPopup(json);
            //return m_currentManager.LoadItem(json);
        }
        // the fields that are visible in the traveler popup
        //public ClientMessage TravelerPopup(string json)
        //{
        //    JSON obj = JSON.Parse(json);
        //    return TravelerPopup(m_travelerManager.FindTraveler(obj["travelerID"]),StationClass.GetStation(obj["station"]));
        //}
        public ClientMessage TravelerPopup(Traveler traveler)
        {
            try
            {
                m_current = traveler;
                // the parameter that returns with all the control events
                string returnParam = new Dictionary<string, string>()
                {
                    {"travelerID", traveler.ID.ToString() }
                }.Stringify();
                //=================================
                // STYLES
                Style spaceBetween = new Style("justify-space-between");
                Style leftAlign = new Style("leftAlign");
                Style rightAlign = new Style("rightAlign");
                Style shadow = new Style("shadow");
                Style white = new Style("white");
                //=================================
                Column fields = new Column(dividers: true);
                if (traveler.Comment != "")
                {
                    Node comment = ControlPanel.FormattedText(traveler.Comment, new Style("red", "shadow", "scrollX", "scrollY"));
                    comment.Style.AddStyle("maxWidth", "300px");
                    fields.Add(new Row(style: spaceBetween)
                        {
                            new TextNode("Comment", style: new Style("leftAlign")), comment
                        }
                    );
                }
                fields.Add(new Row(style: spaceBetween)
                    {
                        new TextNode("State",style: new Style("leftAlign")), new TextNode(traveler.State.ToString(),style: new Style("white","rightAlign","shadow"))
                    }
                );
                fields.Add(new Row(style: spaceBetween)
                    {
                        new TextNode("ID",style: new Style("leftAlign")), new TextNode(traveler.ID.ToString(),style: new Style("yellow","rightAlign","shadow"))
                    }
                );
                //fields.Add(
                //    new Row(justify: "space-between")
                //    {
                //        new TextNode("Type",textAlign: "left"), new TextNode(traveler.GetType().Name,"white","right")
                //    }
                //);
                List<string> stations = StationClass.GetStations().Where(x => x.CreatesThis(traveler)).Select(y => y.Name).ToList();
                stations.Add("Start");
                fields.Add(
                    new Row(style: spaceBetween)
                    {
                        new TextNode("Starting station",style: new Style("leftAlign")), new Selection("Station","MoveTravelerStart",stations,traveler.Station.Name,returnParam)
                    }
                );
                fields.Add(
                    new Row(style: spaceBetween)
                    {
                        new TextNode("Model",style: new Style("leftAlign")), new TextNode(traveler.ItemCode,style:new Style("white","rightAlign","shadow"))
                    }
                );
                if (traveler is Table)
                {
                    fields.Add(
                        new Row(style: spaceBetween)
                        {
                            new TextNode("Shape",style: new Style("leftAlign")), new TextNode((traveler as Table).Shape,style: new Style("white","rightAlign","shadow"))
                        }
                    );
                }

                fields.Add(
                    new Row(style: spaceBetween)
                    {
                        new TextNode("Qty on traveler",style: new Style("leftAlign")), new TextNode(traveler.Quantity.ToString(),style: new Style("white","rightAlign","shadow"))
                    }
                );

                if (traveler.ParentOrderNums.Count == 0)
                {
                    fields.Add(new TextNode("Make to Stock", style: new Style("red", "shadow")));
                }
                else
                {
                    // Orders
                    Column orders = new Column(style: new Style("blackout__popup__controlPanel__list"));
                    orders.Add(new Expand());
                    foreach (string orderNo in traveler.ParentOrderNums)
                    {
                        Order order = traveler.ParentOrders.Find(o => o.SalesOrderNo == orderNo);
                        Node orderListing = (order != null ?
                            (Node)new Row() {
                                (Node)new Button(orderNo, "OrderPopup", @"{""orderNo"":" + order.SalesOrderNo.Quotate() + "}"),
                                new Button("", "RemoveOrderFromTraveler",new JsonObject() { { "order", order.SalesOrderNo } }, style: new Style("deleteBtn"))
                            }
                            
                            : new TextNode(orderNo));
                        orders.Add(orderListing);
                    }
                    fields.Add(
                        new Row(style: spaceBetween)
                        {
                            new TextNode("Orders",style: new Style("leftAlign")), orders
                        }
                    );
                }
                // Parents
                if (traveler.ParentTravelers.Count > 0)
                {
                    Column parents = new Column(style: new Style("blackout__popup__controlPanel__list"));
                    parents.Add(new Expand());
                    foreach (int parentID in traveler.ParentIDs)
                    {
                        Traveler parent = traveler.ParentTravelers.Find(p => p.ID == parentID);
                        Node parentLink = parent != null ? (Node)new Button(parentID.ToString(), "LoadTraveler", "{\"travelerID\":" + parent.ID + "}")
                            : new TextNode(parentID.ToString());
                        parents.Add(parentLink);
                    }
                    fields.Add(
                        new Row(style: spaceBetween)
                        {
                            new TextNode("Parents",style: new Style("leftAlign")), parents
                        }
                    );
                }
                // Children
                if (traveler.ChildTravelers.Count > 0)
                {
                    Column children = new Column(style: new Style("blackout__popup__controlPanel__list"));
                    children.Add(new Expand());
                    foreach (int childID in traveler.ChildIDs)
                    {
                        Traveler parent = traveler.ParentTravelers.Find(p => p.ID == childID);
                        Node childLink = parent != null ? (Node)new Button(childID.ToString(), "LoadTraveler", "{\"travelerID\":" + parent.ID + "}")
                            : new TextNode(childID.ToString());
                        children.Add(childLink);
                    }
                    fields.Add(
                        new Row(style: spaceBetween)
                        {
                            new TextNode("Children",style: new Style("leftAlign")), children
                        }
                    );
                }
                // Items
                if (traveler.Items.Count > 0)
                {
                    Column items = new Column(style: new Style("blackout__popup__controlPanel__list"));
                    items.Add(new Expand());
                    DataTable itemTable = new DataTable();
                    itemTable.Columns.Add(new DataColumn("Item"));
                    itemTable.Columns["Item"].DataType = typeof(Button);
                    itemTable.Columns.Add(new DataColumn("Station"));
                    itemTable.Columns.Add(new DataColumn("State"));
                    itemTable.Columns["State"].DataType = typeof(TextNode);

                    foreach (TravelerItem item in traveler.Items)
                    {
                        DataRow row = itemTable.NewRow();

                        row["Item"] = new Button(traveler.PrintSequenceID(item), "ItemPopup", "{\"travelerID\":" + traveler.ID + ",\"itemID\":" + item.ID + "}");
                        row["Station"] = item.Station.Name;
                        row["State"] = new TextNode(item.PrintState(),item.QueueStyle());

                        itemTable.Rows.Add(row);
                    }
                    items.Add(ControlPanel.CreateDataTable(itemTable));
                    fields.Add(
                        new Row(style: spaceBetween)
                        {
                            new TextNode("Items",style: new Style("leftAlign")), items
                        }
                    );
                }

                Column controls = new Column()
                {
                    new Button("More Info","LoadTravelerJSON",returnParam),
                    new Button("Disintegrate","DisintegrateTraveler",returnParam),
                    new Button("Enter Production","EnterProduction",returnParam),
                    new Button("Finish","Finish",returnParam),
                    new Button("Add Comment","AddComment")
                };

                return new ControlPanel(traveler.GetType().Name, new Row() { fields, controls }).Dispatch();
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error when getting display fields");
            }
        }
        public ClientMessage AddComment(string json)
        {
            try
            {
                Form form = new Form();
                form.Title = "Add Comment";
                form.Textbox("comment", "Comment");
                return form.Dispatch("CommentSubmitted");
            }
            catch (Exception ex)
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

                m_current.Comment +=
                (m_current.Comment.Length > 0 ? "\n" : "") +
                m_user.Name + " ~ " + form.ValueOf("comment");
                m_travelerManager.OnTravelersChanged(new List<Traveler>() { m_current });
                return new ClientMessage();
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error submitting comment");
            }
        }
        // the order popup
        public ClientMessage OrderPopup(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                m_order = Server.OrderManager.FindOrder(obj["orderNo"]);
                if (m_order != null)
                {
                    Column orderPopup = new Column(true);
                    Row controls = new Row()
                    {
                        new Button("Remove Order","RemoveOrder"),
                        new Button("Add Order","AddOrder")
                    };
                    orderPopup.Add(controls);
                    orderPopup.Add(new Row(style: new Style("justify-space-between"))
                        {
                            new TextNode("Ship Date",style: new Style("leftAlign")), new TextNode(m_order.ShipDate.ToString("MM/dd/yyyy"),style: new Style("white","rightAlign","shadow"))
                        }
                    );
                    orderPopup.Add(new Row(style: new Style("justify-space-between"))
                        {
                            new TextNode("Order Date",style: new Style("leftAlign")), new TextNode(m_order.OrderDate.ToString("MM/dd/yyyy"),style: new Style("white","rightAlign","shadow"))
                        }
                    );
                    orderPopup.Add(new Row(style: new Style("justify-space-between"))
                        {
                            new TextNode("Customer",style: new Style("leftAlign")), new TextNode(m_order.CustomerNo,style: new Style("white","rightAlign","shadow"))
                        }
                    );
                    orderPopup.Add(new Row(style: new Style("justify-space-between"))
                        {
                            new TextNode("Status",style: new Style("leftAlign")), new TextNode(m_order.Status.ToString(),style: new Style("white","rightAlign","shadow"))
                        }
                    );
                    NodeList lineItems = new NodeList(DOMtype: "table");

                    // Header
                    NodeList header = new NodeList(DOMtype: "tr");
                    header.Add(new TextNode("Item Code", style: new Style("mediumBorder"), DOMtype: "th"));
                    header.Add(new TextNode("Ordered", style: new Style("mediumBorder"), DOMtype: "th"));
                    header.Add(new TextNode("On Hand", style: new Style("mediumBorder"), DOMtype: "th"));
                    header.Add(new TextNode("Traveler", style: new Style("mediumBorder"), DOMtype: "th"));
                    header.Add(new TextNode("Shipped", style: new Style("mediumBorder"), DOMtype: "th"));
                    lineItems.Add(header);
                    foreach (OrderItem item in m_order.Items)
                    {
                        // Detail
                        NodeList row = new NodeList(DOMtype: "tr");
                        row.Add(new TextNode(item.ItemCode, style: new Style("mediumBorder"), DOMtype: "td"));
                        row.Add(new TextNode(item.QtyOrdered.ToString(), style: new Style("mediumBorder"), DOMtype: "td"));
                        row.Add(new TextNode(InventoryManager.GetMAS(item.ItemCode).ToString(), style: new Style("mediumBorder"), DOMtype: "td"));
                        if (item.ChildTraveler >= 0)
                        {
                            row.Add(new Button(item.ChildTraveler.ToString("D6"), "LoadTraveler", @"{""travelerID"":" + item.ChildTraveler + "}"));
                        }
                        else
                        {
                            row.Add(new Node(style: new Style("mediumBorder"), DOMtype: "td")); // blank if no child traveler
                        }
                        row.Add(new TextNode(item.QtyShipped.ToString(), style: new Style("mediumBorder"), DOMtype: "td"));
                        lineItems.Add(row);
                    }
                    orderPopup.Add(lineItems);
                    return new ControlPanel("Order " + m_order.SalesOrderNo, orderPopup).Dispatch();
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
                SelectedItem = item;
                Column fields = new Column(true);
                fields.Add(
                    new Row(style: new Style("justify-space-between"))
                    {
                        new TextNode("Traveler",style: new Style("leftAlign")), new Button(traveler.ID.ToString(),"LoadTraveler",json)
                    }
                );
                fields.Add(
                    new Row(style: new Style("justify-space-between"))
                    {
                        new TextNode("Station",style: new Style("leftAlign")), new Selection("Station","MoveItem",StationClass.GetStations().Select(s => s.Name).ToList(),item.Station.Name,json)
                    }
                );
                fields.Add(
                    new Row(style: new Style("justify-space-between"))
                    {
                        new TextNode("ItemCode",style: new Style("leftAlign")), new TextNode(item.ItemCode,style: new Style("white","rightAlign","shadow"))
                    }
                );
                fields.Add(
                    new Row(style: new Style("justify-space-between"))
                    {
                        new TextNode("State",style: new Style("leftAlign")), new TextNode(item.LocalState.ToString(),style: new Style("white","rightAlign","shadow"))
                    }
                );
                if (item.History.Count > 0)
                {
                    Column history = new Column(style: new Style("blackout__popup__controlPanel__list"));
                    history.Add(new Expand());
                    int index = 0;
                    foreach (Event evt in item.History)
                    {
                        history.Add(new Button(evt.Date.ToString("MM/dd/yyyy") + " " + (evt is ScrapEvent ? "Scrapped" : evt is ProcessEvent ? ((ProcessEvent)evt).Process.ToString() : evt is LogEvent ? ((LogEvent)evt).LogType.ToString() : "Event"), "EventPopup", "{\"travelerID\":" + traveler.ID + ",\"itemID\":" + item.ID + ",\"eventIndex\":" + index + "}"));
                        index++;
                    }
                    fields.Add(
                        new Row(style: new Style("justify-space-between"))
                        {
                            new TextNode("History",style: new Style("leftAlign")), history
                        }
                    );
                }
                Column controls = new Column()
                {
                    new Button("Print Labels","LabelPopup",json)
                };
                if (!item.Scrapped)
                {
                    foreach (Node node in FlagItemOptions()) controls.Add(node);
                }

                return new ControlPanel(traveler.PrintSequenceID(item), new Row() { fields, controls }).Dispatch(false);
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
                            //item.Station = station;
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
                    new Row(style: new Style("justify-space-between"))
                    {
                        new TextNode("Item",style: new Style("leftAlign")), new Button(traveler.PrintSequenceID(item),"ItemPopup",json)
                    }
                );
                fields.Add(
                    new Row(style: new Style("justify-space-between"))
                    {
                        new TextNode("Date",style: new Style("leftAlign")), new TextNode(evt.Date.ToString("MM/dd/yyyy @ hh:mm tt"),style: new Style("white","rightAlign","shadow"))
                    }
                );
                if (evt is ProcessEvent)
                {
                    ProcessEvent process = (ProcessEvent)evt;
                    fields.Add(
                        new Row(style: new Style("justify-space-between"))
                        {
                        new TextNode("Station",style: new Style("leftAlign")), new TextNode(process.Station.Name,style: new Style("white","rightAlign","shadow"))
                        }
                    );
                    fields.Add(
                        new Row(style: new Style("justify-space-between"))
                        {
                        new TextNode("Process",style: new Style("leftAlign")), new TextNode(process.Process.ToString(),style: new Style("white","rightAlign","shadow"))
                        }
                    );
                    fields.Add(
                        new Row(style: new Style("justify-space-between"))
                        {
                        new TextNode("Duration",style: new Style("leftAlign")), new TextNode(Math.Round(process.Duration,2).ToString() + " min",style: new Style("white","rightAlign","shadow"))
                        }
                    );
                    fields.Add(
                        new Row(style: new Style("justify-space-between"))
                        {
                        new TextNode("User",style: new Style("leftAlign")), new TextNode(process.User.Name,style: new Style("white","rightAlign","shadow"))
                        }
                    );
                }
                else
                {
                    LogEvent log = (LogEvent)evt;
                    fields.Add(
                        new Row(style: new Style("justify-space-between"))
                        {
                        new TextNode("Station",style: new Style("leftAlign")), new TextNode(log.Station.Name,style: new Style("white","rightAlign","shadow"))
                        }
                    );
                    fields.Add(
                        new Row(style: new Style("justify-space-between"))
                        {
                        new TextNode("Log type",style: new Style("leftAlign")), new TextNode(log.LogType.ToString(),style: new Style("white","rightAlign","shadow"))
                        }
                    );
                    if (evt is Documentation)
                    {
                        fields.Add(ControlPanel.CreateDictionary(evt.ExportViewProperties()));
                    }
                }
                return new ControlPanel(traveler.PrintSequenceID(item) + " Event", fields).Dispatch(false);
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error loading item popup");
            }
        }
        private void MultiTravelerOptions()
        {
            try
            {
                //foreach (string selectedID in selectedIDs)
                //{
                //    Traveler traveler = m_currentManager.FindTraveler(Convert.ToInt32(selectedID));

                //}

                Column IDs = new Column(style: new Style("justify-flex-start"));
                foreach (Traveler traveler in m_selected)
                {
                    IDs.Add(new TextNode(traveler.PrintID()));
                }
                Column controls = new Column()
                {
                    new Button("Disintegrate","DisintegrateTraveler"),
                    new Button("Enter Production","EnterProduction"),
                    new TextNode("Starting Station"),
                    new Selection("Starting Station","MoveTravelerStart",StationClass.StationNames())
                };

                ControlPanel panel = new ControlPanel("Travelers", new Row() { IDs, controls });
                SendMessage(new ClientMessage("ControlPanel", panel.ToString()).ToString());
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
            }
        }
        public ClientMessage OptionsMenu(string json)
        {
            try
            {
                Style flexStart = new Style("justify-flex-start");
                Column download = new Column(style: flexStart)
                {
                    new TextNode("Download"),
                    new Button("Traveler Summary","TravelerSummaryForm"),
                    new Button("Production Report", "ExportCSV",new JsonObject() { { "sort", SummarySort.All }, { "type", "Table" }, { "csv", "production" } }),
                    new Button("Partial Production", "ExportCSV",new JsonObject() { { "sort", SummarySort.All }, { "type", "Table" }, { "csv", "partialProduction" } }),
                    new Button("Scrap Report", "ExportCSV",new JsonObject() { { "sort", SummarySort.All }, { "type", "Table" }, { "csv", "scrap" } }),
                    new Button("User Report", "DateRangePopup",@"{""innerCallback"":""DownloadUserSummary""}"),
                    new Button("Rework Report", "ExportRework",@"{""sort"":""All"",""type"":""Table""}"),
                    new Button("Inventory Report","ExportCSV",new JsonObject() { { "sort", "all" }, { "type", "Table" }, { "csv", "inventory" } }),
                    new Button("Custom Report","CustomReportForm")
                };
                Column manage = new Column(style: flexStart)
                {
                    new TextNode("Manage"),
                    new Button("New User","UserForm"),
                    new Button("Edit User","SearchPopup",@"{""interfaceCall"":""EditUserForm"",""message"":""Search for a user by name or ID""}"),
                    new Button("New Traveler","TravelerForm"),
                    new Button("Kanban Monitor", "KanbanMonitor"),
                    new Button("Orders","OrderListPopup"),
                    new Button("Clear Start Queue","ClearStartQueue"),

                    new TextNode("Invoke"),
                    new Button("Refactor Travelers","RefactorTravelers"),
                    new Button("Create Travelers","CreateTravelersForm")
                };
                Column view = new Column(style: flexStart)
                {
                    new TextNode("View"),
                    new Button("View Summary","CreateSummary",@"{""sort"":""Active"",""type"":""Table"",""from"":"""",""to"":""""}")

                };
                ControlPanel panel = new ControlPanel("Options", new Row() { download, manage, view });
                return new ClientMessage("ControlPanel", panel.ToString());
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error when getting display fields");
            }
        }
        public ClientMessage CreateTravelersForm(string json)
        {
            try
            {
                Form form = new Form();
                form.Title = "Filter orders";
                form.Checkbox("tables", "Tables", true);
                form.Textbox("order", "Order Number");
                form.Date("before", "Ship Before");
                form.Date("orderBefore", "Order Before");
                form.Checkbox("consolidate", "Consolidate orders", true);
                form.Checkbox("consolidatePriorityCustomers", "Consolodate priority customers (" + ((JsonArray)JSON.Parse(ConfigManager.Get("priorityCustomers"))).Print() + ") separately<br>", true);
                List<string> customers = new List<string>
                {
                    "ABARGAS","ACEEDUC","ADP    ","AEROBLD","AFC    ","AGILE  ","AJAXSCH","ALABOUT","ALCON  ","ALLGLAS","ALTRA  ","ALUMBAU","AMAZOND","AMERICA","AMTAB  ","ANDERSN","ANIMAL ","AQADVOH","AQRSERV","AQUABUY","AQUADEP","AQUAIMP","AQUALND","AQUARAD","AQUARIA","AQUARIU","AQUATIC","ARVEST ","ATDAMER","ATDCAPL","BARNES ","BASSETT","BEAL   ","BECKER ","BEENEES","BEIMDIK","BIMART ","BLAZARC","BLUEFIN","BLUETHB","BMCOFF ","BOTSON ","BRALEY ","BRANCO ","BRNSNGR","C&HDIST","CAROLIN","CASCO  ","CENTRAL","CENTRPT","CFM    ","CHLDCAR","CHUCK  ","CLAIMS ","CLARK  ","CLAWPAW","COFACE ","COFCROC","COLEMAN","COMMERC","COMPACK","CONTFRN","COSTCO ","COSTCOW","COWPUBS","COYOTE ","CRAFTSH","CREATMK","CREWSMA","CROSBY ","CROSS  ","CROWDER","D&WSALE","DAISYBB","DALEY  ","DALMIDW","DAVCO  ","DAVENPO","DAVIDS ","DAVIDSB","DAVIDSO","DAVIS  ","DETWILE","DFWAQUA","DIABLO ","DONLOLL","DUBOSE ","DWYER  ","EASTSID","EBAY   ","EDUCDEP","ELEGAB ","ERICKSO","EVERFUR","EXFACTO","EXOTIC ","FACTSEL","FCOONEY","FELBAPT","FIECANA","FIESTAD","FINTAST","FISHPLA","FISHTAN","FISHWIS","FOREMAN","FOSTER ","FOURSOP","FRANKS ","FRYE   ","FURNNET","GASKET ","GENERAL","GODSCRE","GODSRES","GREATOU","GRIERIN","GRILLST","GUNLOCK","H&H    ","HARRIS ","HDHAPPY","HEMBREE","HEMBREM","HEMCO  ","HERTZFN","HOMEDEP","HOMETOW","HONCOMP","HOOVER ","HUNTE  ","HUNTER ","IFD    ","IFURN  ","INDOFF ","INTEGFN","IPAEDUC","IQSI   ","JACK'S ","JAMESCH","JAMESSH","JARDEN ","JJAMESO","JMJWORK","JSATRAD","K&S    ","KAMWOOD","KAY12  ","KENDAL ","KLOG   ","KSCONDS","KURTZBR","LAKESQ ","LANDMAN","LARSON ","LATTAS ","LAVACA ","LAZBOY ","LEGGETT","LIBERTY","LOISSCH","LONESTR","LOVELAN","LOWES  ","LOZIER ","MARTIN ","MARTLUT","MATEL  ","MAVIATN","MCALIST","MCCAULE","MCCOOL ","MEIJER ","MENARDS","MIDSTAT","MILLERS","MILLERZ","MILLS' ","MISC   ","MISCELL","MODOR  ","MOP    ","MORETHN","MOSER  ","MSSC   ","NATBOND","NATWIDE","NBFLA  ","NEEL   ","NEOSHOB","NEOSHOD","NEOSHR5","NETSHOP","NEWDISP","NEXTGEN","NOAHS  ","NOBIS  ","NOLANS ","OAKRIDG","OF003  ","OF008  ","OF011  ","OF012  ","OF013  ","OF014  ","OF023  ","OF031  ","OF032  ","OF034  ","OF035  ","OF046  ","OF059  ","OF065  ","OF067  ","OF070  ","OF071  ","OF083  ","OF088  ","OF090  ","OF091  ","OF093  ","OF094  ","OF099  ","OF109  ","OF111  ","OF112  ","OF113  ","OF114  ","OF120  ","OF123  ","OF124  ","OF141  ","OF150  ","OF153  ","OF156  ","OF158  ","OF167  ","OF169  ","OF172  ","OF174  ","OF180  ","OF181  ","OF182  ","OF184  ","OF185  ","OF190  ","OF197  ","OF205  ","OF208  ","OF209  ","OF211  ","OF226  ","OF232  ","OF233  ","OF237  ","OF243  ","OF250  ","OF252  ","OF255  ","OF256  ","OF268  ","OF270  ","OF272  ","OF273  ","OF279  ","OF283  ","OF284  ","OF289  ","OF291  ","OF293  ","OF295  ","OF298  ","OF299  ","OF314  ","OF317  ","OF321  ","OF323  ","OF326  ","OF327  ","OF329  ","OF330  ","OF335  ","OF336  ","OF338  ","OF341  ","OF348  ","OF352  ","OF353  ","OF354  ","OF360  ","OF362  ","OF368  ","OF371  ","OF372  ","OF374  ","OF377  ","OF378  ","OF384  ","OF385  ","OF386  ","OF387  ","OF391  ","OF392  ","OF393  ","OF394  ","OF395  ","OF396  ","OF397  ","OF398  ","OF399  ","OF401  ","OF402  ","OF403  ","OF404  ","OF405  ","OF406  ","OF407  ","OF408  ","OF409  ","OF410  ","OF411  ","OF413  ","OF422  ","OFCCON ","OFFDEP ","OFFDEPF","OFFDEPV","OFFDPBS","OFFMAX ","OFFSOUR","OFFSTAR","OFGPART","OFGWARR","OLDTOWN","OLPI   ","ONEWAYF","OSBORNE","OSULLIV","OVATION","OVERSTO","OZRKPLS","PALLETS","PARTS  ","PAYROLL","PEOPLES","PETCOCO","PETS PA","PETSMAR","PETSPLS","PETSUPE","PETZONE","PLAYTIM","PREWETT","QL     ","R&R MAC","R.G. AP","RACCHRC","REDNECK","REYNOLD","RJRAY  ","RMIND  ","ROEBLNG","ROGARDS","ROGERS ","ROSS   ","RTC    ","SAGECC ","SAMPLES","SAMS   ","SARTINF","SCHAEFE","SCHENKE","SCHLAID","SCHLBOX","SCHLPRD","SCHLSIN","SCLHSPR","SENECR7","SHERWIN","SHICK  ","SHORE  ","SIBLEY ","SMARKET","SPORTS ","SSIFURN","SSWORLD","STANDBY","STAOFMO","STAPLBI","STAPLES","STATELI","STEELMN","STRFRKD","STRONG ","SUNBEAM","TALBOT ","TANKSTO","TARPLEY","TEACHED","TEACHLG","TEENCHA","TEST   ","THOMPSN","THORCO ","TIENLE ","TOEWS  ","TOWNSEN","TPCINC ","TRAVIS ","TROPICL","TURNING","TWIN   ","TXSHCHS","UNBEAT ","UNITY  ","UPDATPT","UPS    ","USTOYCO","VASTMKT","VEASECM","VTINDUS","WALMART","WALMCOM","WAREHSE","WAYFAIR","WBMASON","WES MAT","WESTPOR","WILDSAL","WILPPET","WOODSPE","WORLDS ","WORLDWI","WORTHCN","WORTHDR","WOZEN  ","WYLIE  ","YELLOW ","ZERO   "
                };
                form.Selection("customer", "Customer", customers);
                return form.Dispatch("CreateTravelers");
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error loading filter orders form");
            }
        }
        public async Task<ClientMessage> CreateTravelers(string json)
        {
            try
            {
                Form form = new Form(json);
                DateTime before = DateTime.MaxValue;
                DateTime orderBefore = DateTime.MaxValue;
                List<Order> orders = Server.OrderManager.GetOpenOrders();

                if (form.ValueOf("order") != string.Empty)
                {
                    // Just a single order
                    Order order = Server.OrderManager.FindOrder(form.ValueOf("order"));
                    if (order != null) orders.RemoveAll(o => o.SalesOrderNo != order.SalesOrderNo);
                }
                else
                {
                    if (DateTime.TryParse(form.ValueOf("before"), out before))
                    {
                        // remove all orders that ship on or after this date
                        orders.RemoveAll(o => o.ShipDate >= before);
                    }
                    if (DateTime.TryParse(form.ValueOf("orderBefore"), out orderBefore))
                    {
                        // remove all orders that were ordered on or after this date
                        orders.RemoveAll(o => o.OrderDate >= orderBefore);
                    }
                }
                bool consolidate = Convert.ToBoolean(form.ValueOf("consolidate"));
                bool consolidatePriorityCustomers = Convert.ToBoolean(form.ValueOf("consolidatePriorityCustomers"));

                if (form.ValueOf("customer") != "")
                {
                    // remove all orders that are not the selected customer
                    orders.RemoveAll(o => o.CustomerNo != form.ValueOf("customer"));
                }
                SendMessage(new ClientMessage("Updating","".Quotate()).ToString());
                await Program.server.CreateTravelers(Convert.ToBoolean(form.ValueOf("tables")),consolidate, consolidatePriorityCustomers, orders,delegate(double percent)
                {
                    ReportProgress(percent);
                });
                return new ClientMessage("Info", "Done refactoring PreProcess travelers");
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error creating travelers");
            }

        }
        public ClientMessage ClearStartQueue(string json)
        {
            try
            {
                Server.TravelerManager.ClearStartQueue();
                return new ClientMessage();
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error clearing start queue");
            }
        }
        public ClientMessage RefactorTravelers(string json)
        {
            try
            {
                SendMessage(new ClientMessage("Updating").ToString());
                Program.server.Update();
                return new ClientMessage("Info", "Done refactoring PreProcess travelers");
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error refactoring traveler quantities");
            }
        }
        public ClientMessage SearchPopup(string json)
        {
            try
            {
                return new ClientMessage("SearchPopup", json);
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error reflecting function call");
            }
        }
        public ClientMessage RemoveOrder(string json)
        {
            try
            {
                Order order = m_order;
                m_order = null;
                Server.OrderManager.RemoveOrder(order);
                SendMessage(new ClientMessage("CloseAll").ToString());
                return new ClientMessage("Info", "Order " + order.SalesOrderNo + " removed!");
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error removing order");
            }
        }
        public ClientMessage AddOrder(string json)
        {
            try
            {
                Order order = m_order;
                Server.OrderManager.AddOrder(order);
                return new ClientMessage("Info", "Order " + order.SalesOrderNo + " added!<br>Refactor travelers to apply this change");
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error adding order");
            }
        }
        public ClientMessage RemoveOrderFromTraveler(string json)
        {
            try
            {
                JsonObject param = (JsonObject)JSON.Parse(json);
                Order order = Server.OrderManager.FindOrder(param["order"]);
                if (order != null && m_current != null)
                {
                    Server.OrderManager.RemoveOrder(order, m_current);
                    return new ClientMessage("Info", "Order " + order.SalesOrderNo + " removed from traveler " + m_current.PrintID() + " !");
                }
                else
                {
                    return new ClientMessage("Info", "Something went wrong... :(");
                }
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error removing order from traveler");
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
            try
            {
                List<string> success = new List<string>();
                List<string> failure = new List<string>();
                if (m_current != null) m_selected.Add(m_current);
                foreach (Traveler traveler in m_selected)
                {
                    if (traveler != null)
                    {
                        Server.TravelerManager.RemoveTraveler(traveler,false);
                        success.Add(traveler.PrintID());
                    } else
                    {
                        failure.Add(traveler.PrintID());
                    }
                }
                m_selected.Clear();
                m_current = null;
                CloseAllPopups();
                Server.TravelerManager.OnTravelersChanged();
                return new ClientMessage("Info", (success.Count > 0 ? "Disintegrated: " + success.Stringify<string>(false) : "") + (failure.Count > 0 ? "<br>Failed to disintegrate: " + failure.Stringify<string>(false) : ""));
            }
            catch (Exception ex)
            {
                m_selected.Clear();
                m_current = null;
                CloseAllPopups();
                Server.LogException(ex);
                return new ClientMessage("Info", "Error Disintegrating traveler");
            }
        }
        public ClientMessage EnterProduction(string json)
        {
            if (m_current != null) m_selected.Add(m_current);
            foreach (Traveler traveler in m_selected)
            {
                traveler.EnterProduction(m_travelerManager);
            }
            m_selected.Clear();
            m_current = null;
            CloseAllPopups();
            Server.TravelerManager.OnTravelersChanged();
            return new ClientMessage();
        }
        public ClientMessage Finish(string json)
        {
            try
            {
                if (SelectedTraveler != null)
                {

                    foreach (TravelerItem item in SelectedTraveler.Items)
                    {
                        if (!item.Finished)
                        {
                            item.Finish(m_user,false);
                        }
                    }
                    SelectedTraveler.UpdateState();
                    Server.TravelerManager.OnTravelersChanged(SelectedTraveler);
                }
                
                if (SelectedTraveler != null && SelectedTraveler.Finished)
                {
                    return new ClientMessage("Info", "Successfully finished all items");
                }
                else
                {
                    return new ClientMessage("Info", "Successfully finished existing items");
                }

            }
            catch (Exception ex)
            {
                m_selected.Clear();
                m_current = null;
                CloseAllPopups();
                Server.LogException(ex);
                return new ClientMessage("Info", "Error Disintegrating traveler");
            }
        }
        public ClientMessage DownloadSummary(string json)
        {
            return m_travelerManager.DownloadSummary(json);
        }
        public ClientMessage DownloadUserSummary(string json)
        {
            try
            {
                //Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                DateTime A = DateTime.MinValue;
                DateTime B = DateTime.MaxValue;
                Form.DateRange(new Form(json), out A, out B);
                return new ClientMessage("Redirect", new Summary(A, B).UserCSV().Quotate());
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
                JsonObject obj = (JsonObject)JSON.Parse(json);
                return Form.DateRange().Dispatch(obj["innerCallback"]);
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
                JsonObject parameters = (JsonObject)JSON.Parse(json);
                bool filterReadyToShip = parameters.ContainsKey("filterReadyToShip") ? (bool)parameters["filterReadyToShip"] : false;
                List<Order> orders = new List<Order>();
                if (filterReadyToShip)
                {
                    orders = Server.OrderManager.GetOrders.Where(
                        o => o.Items.Exists(i => i.ChildTraveler >= 0 && InventoryManager.GetMAS(i.ItemCode) >= i.QtyNeeded)
                        ).ToList();
                }
                else
                {
                    orders = Server.OrderManager.GetOrders.Where(o => o.Items.Exists(i => i.ChildTraveler >= 0)).ToList();
                }
                Column list = new Column(true, style: new Style("scrollY"));
                foreach (Order order in orders)
                {
                    list.Add(new Button(order.SalesOrderNo, "OrderPopup", @"{""orderNo"":" + order.SalesOrderNo.Quotate() + "}"));
                }
                Column controls = new Column()
                {
                    new Button("Ready to Ship","OrderListPopup",new JsonObject() { {"filterReadyToShip",!filterReadyToShip } })
                };
                return new ClientMessage("ControlPanel", new ControlPanel("Orders", new Row() { controls, list }).ToString());
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
                //Traveler traveler = m_currentManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                Form form = new Form();
                form.Name = "Print Labels";
                form.Selection("labelType", "Label Type", ExtensionMethods.GetNames<LabelType>());
                form.Integer("quantity", "Quantity", 1, 100, 1);
                form.Selection("printer", "Printer", ((JsonArray)JSON.Parse(ConfigManager.Get("printers"))).ToList());
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
                TravelerItem item = traveler.FindItem(Convert.ToUInt16(parameters["itemID"]));
                int qty = Convert.ToInt32(form.ValueOf("quantity"));
                return new ClientMessage("Info", traveler.PrintLabel(item.ID, (LabelType)Enum.Parse(typeof(LabelType), form.ValueOf("labelType")), qty > 0 ? qty : 1, true,printer:form.ValueOf("printer")));
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Could not print label(s) due to a pesky error :(");
            }
        }
        //public ClientMessage ExportProduction(string json)
        //{
        //    ClientMessage returnMessage = new ClientMessage();
        //    try
        //    {
        //        Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
        //        Summary summary = new Summary(m_travelerManager as ITravelerManager, obj["type"], (SummarySort)Enum.Parse(typeof(SummarySort), obj["sort"]));
        //        string downloadLocation = summary.ProductionCSV();
        //        returnMessage = new ClientMessage("Redirect", downloadLocation.Quotate());
        //    }
        //    catch (Exception ex)
        //    {
        //        Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
        //        returnMessage = new ClientMessage("Info", "error");
        //    }
        //    return returnMessage;
        //}
        //public ClientMessage ExportPartialProduction(string json)
        //{
        //    ClientMessage returnMessage = new ClientMessage();
        //    try
        //    {
        //        Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
        //        Summary summary = new Summary(m_travelerManager as ITravelerManager, obj["type"], (SummarySort)Enum.Parse(typeof(SummarySort), obj["sort"]));
        //        string downloadLocation = summary.PartialProductionCSV();
        //        returnMessage = new ClientMessage("Redirect", downloadLocation.Quotate());
        //    }
        //    catch (Exception ex)
        //    {
        //        Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
        //        returnMessage = new ClientMessage("Info", "error");
        //    }
        //    return returnMessage;
        //}
        public ClientMessage ExportCSV(string json)
        {
            ClientMessage returnMessage = new ClientMessage();
            try
            {
                JsonObject obj = (JsonObject)JSON.Parse(json);
                Summary summary = new Summary(m_travelerManager as ITravelerManager, obj["type"], (SummarySort)Enum.Parse(typeof(SummarySort), obj["sort"]));


                string downloadLocation = "";
                string csv = obj.ContainsKey("csv") ? (string)obj["csv"] : "";
                switch (csv)
                {
                    case "production":
                        downloadLocation = summary.ProductionCSV(); break;
                    case "scrap":
                        downloadLocation = summary.ScrapCSV(); break;
                    case "partialProduction":
                        downloadLocation = summary.PartialProductionCSV(); break;
                    case "inventory":
                        downloadLocation = summary.InventorySummary(); break;
                }
                returnMessage = new ClientMessage("Redirect", downloadLocation.Quotate());
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
                returnMessage = new ClientMessage("Info", "error");
            }
            return returnMessage;
        }
        public ClientMessage TravelerSummaryForm(string json)
        {
            //JsonObject obj = (JsonObject)JSON.Parse(json);
            //Summary summary = new Summary(m_travelerManager as ITravelerManager, obj["type"], (SummarySort)Enum.Parse(typeof(SummarySort), obj["sort"]));
            Form form = new Form();
            form.Title = "Traveler Summary";
            form.Selection("summary", "Summary type", ExtensionMethods.GetNames<SummaryType>(), SummaryType.Traveler.ToString());
            form.Radio("type", "Type", new List<string>() { "Table" },"Table");
            form.Radio("state", "State", ExtensionMethods.GetNames<GlobalItemState>(), GlobalItemState.PreProcess.ToString());
            form.Selection("station", "Station", StationClass.StationNames());
            return form.Dispatch("TravelerSummary");
        }
        public ClientMessage TravelerSummary(string json)
        {
            Form form = new Form(json);
            Summary summary = new Summary(m_travelerManager as ITravelerManager, form.ValueOf("type"), (GlobalItemState)Enum.Parse(typeof(GlobalItemState), form.ValueOf("state")),StationClass.GetStation(form.ValueOf("station")));
            //string downloadLocation = summary.ExportCSV((SummaryType)Enum.Parse(typeof(SummaryType), form.ValueOf("summary")));
            return new ClientMessage("Redirect",summary.ExportCSV((SummaryType)Enum.Parse(typeof(SummaryType), form.ValueOf("summary"))).Quotate());
        }
        //public ClientMessage ExportScrap(string json)
        //{
        //    //ClientMessage returnMessage = new ClientMessage();
        //    //try
        //    //{
        //    //    Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
        //    //    Summary summary = new Summary(m_currentManager as ITravelerManager, obj["type"], (SummarySort)Enum.Parse(typeof(SummarySort), obj["sort"]));
        //    //    string downloadLocation = summary.CSV("test.csv", new List<SummaryColumn>() {
        //    //        new SummaryColumn("ID","ID"),
        //    //        new SummaryColumn("ItemCode","ItemCode")
        //    //    });
        //    //    returnMessage = new ClientMessage("Redirect", downloadLocation.Quotate());
        //    //}
        //    //catch (Exception ex)
        //    //{
        //    //    Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
        //    //    returnMessage = new ClientMessage("Info", "error");
        //    //}
        //    //return returnMessage;
        //    ClientMessage returnMessage = new ClientMessage();
        //    try
        //    {
        //        Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
        //        Summary summary = new Summary(m_travelerManager as ITravelerManager, obj["type"], (SummarySort)Enum.Parse(typeof(SummarySort), obj["sort"]));
        //        string downloadLocation = summary.ScrapCSV();
        //        returnMessage = new ClientMessage("Redirect", downloadLocation.Quotate());
        //    }
        //    catch (Exception ex)
        //    {
        //        Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
        //        returnMessage = new ClientMessage("Info", "error");
        //    }
        //    return returnMessage;
        //}
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
        //public ClientMessage ExportInventory(string json)
        //{
        //    ClientMessage returnMessage = new ClientMessage();
        //    try
        //    {
        //        Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
        //        Summary summary = new Summary(m_travelerManager as ITravelerManager, obj["type"], (SummarySort)Enum.Parse(typeof(SummarySort), obj["sort"]));
        //        string downloadLocation = summary.InventorySummary();
        //        returnMessage = new ClientMessage("Redirect", downloadLocation.Quotate());
        //    }
        //    catch (Exception ex)
        //    {
        //        Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
        //        returnMessage = new ClientMessage("Info", "error");
        //    }
        //    return returnMessage;
        //}
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
                }
                else
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
                }
                else
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
                    return new ClientMessage("EditUserForm", user.CreateFilledForm().ToString());
                }
                else
                {
                    return new ClientMessage("Info", "Could not find the requested user");
                }
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "error");
            }
        }
        public ClientMessage ListTravelers(List<Traveler> travelers)
        {
            Column column = new Column();
            foreach (Traveler traveler in travelers)
            {
                column.Add(new Button(traveler.PrintID(), "LoadTraveler", new JsonObject() { { "travelerID", traveler.ID } }));
            }
            return new ControlPanel("Travelers", column).Dispatch();
        }
        public new ClientMessage SearchSubmitted(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                string searchPhrase = obj["searchPhrase"];
                //string[] parts = obj["searchPhrase"].Split('-');

                string[] parts = searchPhrase.Split('-');
                if ((parts.Length == 1 || parts.Length == 2) && parts[0].Length <= 6) {
                    int travelerID;
                    if (int.TryParse(parts[0],out travelerID)) {
                        Traveler traveler = Server.TravelerManager.FindTraveler(travelerID);
                        if (traveler != null)
                        {
                            ushort itemID;
                            if (parts.Length == 2 && ushort.TryParse(parts[1],out itemID))
                            {
                                TravelerItem item = traveler.FindItem(itemID);
                                if (item != null)
                                {
                                    SendMessage(new ClientMessage("ClearSearch"));
                                    return ItemPopup(@"{""travelerID"":" + traveler.ID + @",""itemID"":" + itemID + "}");
                                } else
                                {
                                    // Try to look up legacy item
                                }
                            }
                            SendMessage(new ClientMessage("ClearSearch"));
                            return TravelerPopup(traveler);
                        } else
                        {
                            // Try to look up legacy traveler
                            return LegacyTravelerPopup(travelerID);

                        }
                    }
                }
                // try to find a model
                if (Traveler.IsTable(searchPhrase) || Traveler.IsChair(searchPhrase))
                {
                    SendMessage(new ClientMessage("ClearSearch"));
                    return ListTravelers(Server.TravelerManager.GetTravelers.Where(t => t.ItemCode.Equals(searchPhrase,StringComparison.CurrentCultureIgnoreCase)).ToList());
                }
                // try to find an order
                Order order = Server.OrderManager.FindOrder(searchPhrase);
                if (order != null)
                {
                    SendMessage(new ClientMessage("ClearSearch"));
                    return OrderPopup(new JsonObject() { { "orderNo", order.SalesOrderNo } });
                }
                return new ClientMessage("Info", "Could not identify a search target");
            }
            catch (Exception ex)
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

        public ClientMessage CustomReportForm(string json)
        {
            try
            {
                Form form = new Form();
                form.Title = "Custom Report";
                form.Selection("subject", "Subject", new List<string>()
                {
                    "Traveler",
                    "User",
                    "Inventory"
                });
                form.Date("from", "From");
                form.Date("to", "To");
                return form.Dispatch("CustomReport");
                
            } catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error loading custom report form");
            }
        }
        public ClientMessage CustomReport(string json)
        {
            try
            {
                Form form = new Form(json);
                DateTime from = DateTime.Today;
                DateTime to = DateTime.Today;
                Form.DateRange(form, out from, out to);
                Summary summary = new Summary(from, to);

                string downloadLocation = "";
                switch (form.ValueOf("subject"))
                {
                    case "Traveler": downloadLocation = summary.ProductionCSV(); break;
                    case "User": downloadLocation = summary.UserCSV(); break;
                    case "Inventory": downloadLocation = summary.InventorySummary(); break;
                }
                if (downloadLocation != String.Empty)
                {
                    return new ClientMessage("Redirect", downloadLocation.Quotate());
                }
                return new ClientMessage();
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error loading custom report form");
            }
        }
        #endregion
        //-----------------------------------
        #region Properties
        private Form m_viewFilter;

        private Order m_order;
        private List<Traveler> m_selected;
        private Traveler m_current = null;
        private StationClass m_currentStation = null;

        public Form ViewFilter
        {
            get
            {
                return m_viewFilter;
            }

            set
            {
                m_viewFilter = value;
            }
        }
        #endregion
        //----------
        // Events
        //----------
        public event TravelersChangedSubscriber TravelersChanged;
    }
}
