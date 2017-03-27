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
    class SupervisorClient : Client, ITravelers
    {
        //------------------------------
        // Public members
        //------------------------------
        public SupervisorClient(TcpClient client, ISupervisor travelerCore) : base(client)
        {
            m_travelerManager = travelerCore;
            m_travelers = m_travelerManager.GetTravelers;
            string stationList = "";
            foreach (StationClass station in StationClass.Stations)
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
                message = message.Trim('"');
                if (message.Length == 0) throw new Exception("bad message");
                StringStream ss = new StringStream(message);
                Dictionary<string, string> obj = ss.ParseJSON();
                if (obj.ContainsKey("interfaceMethod"))
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
            }
            catch (Exception ex)
            {
                // something went wrong, it is best to just listen for a new message
            }
            ListenAsync();
        }
        public void HandleTravelersChanged(List<Traveler> travelers)
        {
            bool mirror = travelers.Count == m_travelerManager.GetTravelers.Count;
            string message = @"{""travelers"":[";
            string travelerJSON = "";
            foreach (Traveler traveler in travelers)
            {
                travelerJSON += (travelerJSON.Length > 0 ? "," : "") + traveler.Export(this.GetType().Name, -1);
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
        protected ISupervisor m_travelerManager;
        protected List<Traveler> m_travelers;

        public ISupervisor TravelerManager
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
