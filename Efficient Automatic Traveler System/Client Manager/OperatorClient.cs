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
    class OperatorClient : Client, IOperator, ITravelers
    {
        //------------------------------
        // Public members
        //------------------------------
        public OperatorClient (TcpClient client, ITravelerManager travelerManager) : base(client)
        {
            m_travelerManager = travelerManager;
            SendMessage((new ClientMessage("InitStations", StationClass.GetStations().Stringify())).ToString());
        }

        public string SetStation(string json)
        {
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                m_station = StationClass.GetStation(obj["station"]);
                HandleTravelersChanged(m_travelerManager.GetTravelers);
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
            }
            return "";
        }
        public void HandleTravelersChanged(List<Traveler> travelers)
        {
            // get the list of travelers that have items at this station
            List<Traveler> stationSpecific = travelers.Where(x => x.State == ItemState.InProcess && (x.QuantityPendingAt(m_station) > 0 || x.QuantityAt(m_station) > 0)).ToList();
            bool mirror = (stationSpecific.Count < travelers.Count);
            if (mirror)
            {
                stationSpecific = m_travelerManager.GetTravelers.Where(x => x.State == ItemState.InProcess && (x.QuantityPendingAt(m_station) > 0 || x.QuantityAt(m_station) > 0)).ToList();
            }
            string message = @"{""travelers"":[";
            string travelerJSON = "";
            foreach (Traveler traveler in stationSpecific)
            {
                if (traveler.GetType().Name == "Table")
                {
                    travelerJSON += (travelerJSON.Length != 0 ? "," : "") + ((Table)traveler).Export(this.GetType().Name,m_station);
                }
                else if (traveler.GetType().Name == "Chair")
                {
                    travelerJSON += (travelerJSON.Length != 0 ? "," : "") + ((Chair)traveler).Export(this.GetType().Name,m_station);
                }
            }
            message += travelerJSON + "],";
            message += "\"mirror\":" + mirror.ToString().ToLower();
            message += "}";
            SendMessage(message);
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
                    m_user.Login(StationClass.GetStation(obj["station"]));
                    return new ClientMessage("LoginSuccess", paramObj.Stringify());
                } else
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

        public ClientMessage AddTravelerEvent(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                ProcessEvent evt = new ProcessEvent(m_user, m_station, traveler.GetCurrentLabor() - Convert.ToDouble(obj["time"]), (ProcessType)Enum.Parse(typeof(ProcessType), obj["eventType"]));
                
                TravelerItem item = (obj["itemID"] != "undefined" ? traveler.FindItem(Convert.ToUInt16(obj["itemID"])) : null);
                return m_travelerManager.AddTravelerEvent(evt,traveler,item);
            } catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error occured");
            }
        }
        public ClientMessage ScrapEvent(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                ScrapEvent evt = new ScrapEvent(m_user, m_station, traveler.GetCurrentLabor() - Convert.ToDouble(obj["time"]), Convert.ToBoolean(obj["startedWork"].ToLower()),obj["reason"]);

                TravelerItem item = (obj["itemID"] != "undefined" ? traveler.FindItem(Convert.ToUInt16(obj["itemID"])) : null);
                return m_travelerManager.AddTravelerEvent(evt, traveler, item);
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
                string scrapReport = ConfigManager.Get("scrapReport");
                return new ClientMessage("DisplayScrapReport", scrapReport);
            } catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error occured");
            }
        }
        public ClientMessage SubmitTraveler(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();

                return m_travelerManager.SubmitTraveler(
                    m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"])),
                    m_station
                );
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info","Error occured");
            }
        }

        public ClientMessage OpenDrawing(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                return new ClientMessage("Redirect", ("../drawings/" + traveler.Part.DrawingNo.Split('-')[0] + ".pdf").Quotate());
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error occured");
            }
        }
        public ClientMessage LoadTraveler(string json)
        {
            Traveler freshTraveler = m_travelerManager.FindTraveler(Convert.ToInt32(new StringStream(json).ParseJSON()["travelerID"]));
            if (freshTraveler != null && ( m_current == null || freshTraveler.ID != m_current.ID))
            {
                DisplayChecklist();
            }
            m_current = freshTraveler;
            return new ClientMessage("LoadTraveler",m_current.Export("OperatorClient",m_station));
        }
        public ClientMessage LoadTravelerJSON(string json)
        {
            return m_travelerManager.LoadTravelerJSON(json);
        }
        public ClientMessage LoadItem(string json)
        {
            Traveler freshTraveler = m_travelerManager.FindTraveler(Convert.ToInt32(new StringStream(json).ParseJSON()["travelerID"]));
            if (freshTraveler != null && m_current != null && freshTraveler.ID != m_current.ID)
            {
                DisplayChecklist();
            }
            m_current = freshTraveler;
            return m_travelerManager.LoadItem(json);
        }
    }
}
