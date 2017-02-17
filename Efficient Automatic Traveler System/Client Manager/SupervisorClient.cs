using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public SupervisorClient(TcpClient client, ref List<Traveler> travelers) : base(client)
        {
            m_travelers = travelers;
            string stationList = "";
            foreach (string station in Traveler.Stations.Keys)
            {
                stationList += (stationList.Length != 0 ? "," : "") + '"' + station + '"';
            }
            SendMessage(@"{""stationList"":[" + stationList + "]}");
            HandleTravelersChanged();
        }
        public virtual async void ListenAsync()
        {
            try
            {
                string message = await RecieveMessageAsync();
                if (!Connected) return;
                if (message.Length == 0) throw new Exception("bad message");
                if (message[0] == '"') message = message.Remove(0, 1);
                StringStream ss = new StringStream(message);
                Dictionary<string, string> obj = ss.ParseJSON();
                if (obj.ContainsKey("completed") && obj.ContainsKey("destination") && obj.ContainsKey("time") && obj.ContainsKey("quantity"))
                {
                    //----------------------
                    // Traveler Completed
                    //----------------------
                    for (int i = 0; i < m_travelers.Count; i++)
                    {
                        if (m_travelers[i].ID.ToString("D6") == obj["completed"].Trim('"'))
                        {
                            m_travelers[i].Station = Traveler.GetStation(obj["destination"].Trim('"'));
                            m_travelers[i].Advance();
                            // log this event
                            m_travelers[i].History.Add(new Event(TravelerEvent.Completed, m_travelers[i].Quantity, m_travelers[i].Station, Convert.ToDouble(obj["time"].Trim('"'))));
                            break;
                        }
                    }
                    TravelersChanged();
                }
            }
            catch (Exception ex)
            {
                // something went wrong, it is best to just listen for a new message
            }
            ListenAsync();
        }
        public void HandleTravelersChanged()
        {
            string message = @"{""travelers"":[";
            string travelerJSON = "";
            foreach (Traveler traveler in m_travelers)
            {
                travelerJSON += (travelerJSON.Length != 0 ? "," : "") + traveler.Export(this.GetType().Name);
            }
            message += travelerJSON + "]}";
            SendMessage(message);
        }
        //------------------------------
        // Private members
        //------------------------------
        //------------------------------
        // Properties
        //------------------------------

        protected List<Traveler> m_travelers;
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
        //----------
        // Events
        //----------
        public event TravelersChangedSubscriber TravelersChanged;
    }
}
