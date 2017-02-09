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
    class OperatorClient : Client
    {
        //------------------------------
        // Public members
        //------------------------------
        public OperatorClient (TcpClient client, ref List<Traveler> travelers) : base(client)
        {
            m_travelers = travelers;
            string stationList = "";
            foreach(string station in Traveler.Stations.Keys)
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
                if (!Connected) throw new Exception("Lost Connection");
                if (message[0] == '"') message = message.Remove(0, 1);
                StringStream ss = new StringStream(message);
                Dictionary<string, string> obj = ss.ParseJSON();
                if (obj.ContainsKey("station"))
                {
                    m_station = Traveler.GetStation(obj["station"].Trim('"'));
                    HandleTravelersChanged();
                } else if (obj.ContainsKey("completed") && obj.ContainsKey("destination") && obj.ContainsKey("time"))
                {
                    //----------------------
                    // Traveler Completed
                    //----------------------
                    for (int i = 0; i < m_travelers.Count; i++)
                    {
                        if (m_travelers[i].ID.ToString("D6") == obj["completed"].Trim('"'))
                        {
                            Table table 
                            traveler.Advance();
                            traveler.Station = Traveler.GetStation(obj["destination"].Trim('"'));
                            // log this event
                            traveler.History.Add(new Event(TravelerEvent.Completed,traveler.Quantity,traveler.Station,Convert.ToDouble(obj["time"].Trim('"'))));
                            break;
                        }
                    }
                    TravelersChanged();
                }
                ListenAsync();
            }
            catch (Exception ex)
            {
            }
            
        }
        public void HandleTravelersChanged()
        {
            List<Traveler> stationSpecific = m_travelers.Where(x => x.Station == m_station).ToList();
            string message = @"{""travelers"":[";
            string travelerJSON = "";
            foreach (Traveler traveler in stationSpecific)
            {
                if (traveler.GetType().Name == "Table")
                {
                    travelerJSON += (travelerJSON.Length != 0 ? "," : "") + ((Table)traveler).Export(traveler.Station);
                }
                else if (traveler.GetType().Name == "Chair")
                {
                    travelerJSON += (travelerJSON.Length != 0 ? "," : "") + ((Chair)traveler).Export();
                }
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
