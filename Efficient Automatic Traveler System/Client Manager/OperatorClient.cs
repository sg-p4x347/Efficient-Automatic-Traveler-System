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
            string stationList = "";
            foreach(StationClass station in StationClass.Stations)
            {
                stationList += (stationList.Length != 0 ? "," : "") + '"' + station.Name + '"';
            }
            SendMessage(@"{""stationList"":" + StationClass.Stations.Stringify() + "}");
            HandleTravelersChanged(m_travelerManager.GetTravelers);
        }
        public virtual async void ListenAsync()
        {
            try
            {
                string message = await RecieveMessageAsync();
                if (!Connected) return;
                if (message.Length == 0) throw new Exception("bad message");
                message = message.Trim('"');
                StringStream ss = new StringStream(message);
                Dictionary<string, string> obj = ss.ParseJSON();

                if (obj.ContainsKey("station"))
                {
                    m_station = Convert.ToInt32(obj["station"]);
                    HandleTravelersChanged(m_travelerManager.GetTravelers);
                } else if (obj.ContainsKey("interfaceMethod"))
                {
                    PropertyInfo pi = this.GetType().GetProperty(obj["interfaceTarget"]);
                    if (pi != null)
                    {
                        MethodInfo mi = pi.GetValue(this).GetType().GetMethod(obj["interfaceMethod"]);
                        if (mi != null)
                        {
                            string returnMessage = (string)mi.Invoke(pi.GetValue(this), new object[] { obj["parameters"] });
                            if (returnMessage != null && returnMessage != "") SendMessage("{\"confirmation\":\"" + returnMessage + "\"}");
                        }
                    }
                }
            } catch (Exception ex)
            {
                // something went wrong, it is best to just listen for a new message
            }
            ListenAsync();
        }
        public void HandleTravelersChanged(List<Traveler> travelers)
        {
            // get the list of travelers that have items at this station
            List<Traveler> stationSpecific = travelers.Where(x => x.QuantityPendingAt(m_station) > 0 || x.QuantityAt(m_station) > 0).ToList();
            bool mirror = (stationSpecific.Count < travelers.Count);
            if (mirror)
            {
                stationSpecific = TravelerManager.GetTravelers.Where(x => x.QuantityPendingAt(m_station) > 0 || x.QuantityAt(m_station) > 0).ToList();
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
        protected int m_station;
        
        internal int Station
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
        public IOperator TravelerManager
        {
            get
            {
                return m_travelerManager;
            }
        }
        //----------
        // Events
        //----------
        public event TravelersChangedSubscriber TravelersChanged;
    }
}
