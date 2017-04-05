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
    class OperatorClient : Client, ITravelers
    {
        //------------------------------
        // Public members
        //------------------------------
        public OperatorClient (TcpClient client, IOperator travelerManager) : base(client)
        {
            m_travelerManager = travelerManager;
            SendMessage((new ClientMessage("InitStations",ConfigManager.Get("stations"))).ToString());
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
                stationSpecific = TravelerManager.GetTravelers.Where(x => x.State == ItemState.InProcess && (x.QuantityPendingAt(m_station) > 0 || x.QuantityAt(m_station) > 0)).ToList();
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
        //------------------------------
        // Properties
        //------------------------------
        protected IOperator m_travelerManager;
        protected StationClass m_station;
        
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
        public IOperator TravelerManager
        {
            get
            {
                return m_travelerManager;
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
    }
}
