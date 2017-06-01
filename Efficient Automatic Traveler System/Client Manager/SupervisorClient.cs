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
            List<string> travelerStrings = new List<string>();

            foreach (Traveler traveler in travelers.Where(x => x.State == m_viewState || x.Items.Exists(y => y.State == m_viewState)))
            {
                
                travelerStrings.Add(ExportTraveler(traveler));
                //travelerStrings.Add(traveler.Export(this.GetType().Name, traveler.Station));
            }
            message.Add("travelers", travelerStrings.Stringify(false));
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
        private string ExportTraveler(Traveler traveler)
        {
            Dictionary<string, string> travelerJSON = new StringStream(traveler.ToString()).ParseJSON(false);
            Dictionary<string, string> stations = new Dictionary<string, string>();
            List<StationClass> stationsToDisplay = traveler.CurrentStations();
            //if (m_viewState == ItemState.PreProcess) stationsToDisplay.Add(StationClass.GetStation("Start"));
            foreach (StationClass station in stationsToDisplay)
            {
                stations.Add(station.Name, traveler.ExportStationSummary(station));
            }
            Dictionary<string, string> stationsObj = new Dictionary<string, string>();
            stationsObj.Add("stations", stations.Stringify());
            travelerJSON.Merge(stationsObj);
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
        public string SetViewFilter(string json)
        {
            ClientMessage returnMessage = new ClientMessage();
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                m_viewState = (ItemState)Enum.Parse(typeof(ItemState), obj["viewState"]);
                HandleTravelersChanged(m_travelerManager.GetTravelers);
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                returnMessage = new ClientMessage("Info","Error configuring view settings");
            }
            return returnMessage.ToString();
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


                Dictionary<string, string> exportedProps = new StringStream(ExportTraveler(traveler)).ParseJSON(false);
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
            return m_travelerManager.LoadItem(json);
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
                Dictionary<string, string> scroll = new Dictionary<string, string>() {
                    { "maxHeight", @"""3em"""},
                    { "overflow-y", @"""auto""" }
                };
                //=================================
                Column fields = new Column(dividers: true);
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
                    Column orders = new Column(style: scroll);
                    foreach (Order order in traveler.ParentOrders)
                    {
                        orders.Add(new TextNode(order.SalesOrderNo));
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
                    Column parents = new Column(style: scroll);
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
                    Column children = new Column(style: scroll);
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
                            new TextNode("Station",styleClasses: new Style("leftAlign")), new TextNode(traveler.Station.Name,styleClasses: new Style("white","rightAlign","shadow"))
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

                Column controls = new Column()
                {
                    new Button("More Info","LoadTravelerJSON",returnParam),
                    new Button("Disintegrate","DisintegrateTraveler",returnParam),
                    new Button("Enter Production","EnterProduction",returnParam),
                    new Button("Print Labels","LabelPopup",returnParam)
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
                    
                    new Button("Download Pre-Process Tables","DownloadSummary",@"{""sort"":""PreProcess"",""type"":""Table""}"),
                    new Button("Download Production", "ExportProduction",@"{""sort"":""All"",""type"":""Table""}"),
                    new Button("Download Scrap", "ExportScrap",@"{""sort"":""All"",""type"":""Table""}")
                };
                Column manage = new Column(style: flexStart)
                {
                    new TextNode("Manage"),
                    new Button("New User","UserForm"),
                    new Button("Edit User","SearchPopup",@"{""interfaceCall"":""EditUserForm"",""message"":""Search for a user by name or ID""}"),
                    new TextNode(""),
                    new Button("New Traveler","TravelerForm"),
                    new Button("Kanban Monitor", "KanbanMonitor")
                };
                Column view = new Column(style: flexStart)
                {
                    new TextNode("View"),
                    new Button("View Summary","CreateSummary",@"{""sort"":""Active"",""type"":""Table"",""from"":"""",""to"":""""}")
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
        public ClientMessage LabelPopup(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                return new ClientMessage("PrintLabelPopup", traveler.ToString());
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
                int qty = Convert.ToInt32(obj["quantity"]);
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                return new ClientMessage("Info",traveler.PrintLabel(Convert.ToUInt16(obj["itemID"]), (LabelType)Enum.Parse(typeof(LabelType), obj["labelType"]),qty > 0 ? qty : 1,true));
                
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
                Form form = new Form(json);
                User newUser = new User(form);
                User existingUser = UserManager.Find(newUser.UID);
                if (existingUser == null)
                {
                    // This is a brand spanking new user!
                    UserManager.AddUser(newUser);
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
                Form form = new Form(json);
                User newUser = new User(form);
                User existingUser = UserManager.Find(newUser.UID);
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
                User user = UserManager.Find(obj["searchPhrase"]);
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
        public ClientMessage NewKanbanItemForm(string json)
        {
            return KanbanManager.NewKanbanItemForm(json);
        }
        public async Task<ClientMessage> NewKanbanItem(string json)
        {
            return await KanbanManager.NewKanbanItem(json);
        }
       
        #endregion
        //-----------------------------------
        #region Properties
        private ItemState m_viewState;
        #endregion
        //----------
        // Events
        //----------
        public event TravelersChangedSubscriber TravelersChanged;
    }
}
