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
                Dictionary<string, string> returnObj = new Dictionary<string, string>();
                Dictionary<string, string> fields = new Dictionary<string, string>();
                fields.Add("ID", (traveler is TableBox ? traveler.ParentTravelers[0].ID : traveler.ID).ToString());
                fields.Add("Type", traveler.GetType().Name.Quotate());
                if (!(traveler is Box))
                {
                    fields.Add("Model", (traveler as IPart).ItemCode.Quotate());
                }
                fields.Add("Qty on traveler", traveler.Quantity.ToString());
                if (station != null)
                {
                    fields.Add("Station", station.Name.Quotate());
                    fields.Add("Pending", traveler.QuantityPendingAt(station).ToString());
                    if (traveler.QuantityCompleteAt(station) > 0)
                    {
                        fields.Add("Complete", traveler.QuantityCompleteAt(station).ToString());
                    }
                }
                returnObj.Add("displayFields", fields.Stringify());
                returnObj.Add("object", traveler.ToString());
                return new ClientMessage("TravelerPopup", returnObj.Stringify());
            } catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info","Error when getting display fields");
            }
        }
        //
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
        public ClientMessage NewTraveler(string json)
        {
            return m_travelerManager.NewTraveler(json);
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
