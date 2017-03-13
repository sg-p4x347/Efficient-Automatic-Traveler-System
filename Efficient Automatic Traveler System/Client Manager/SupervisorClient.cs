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
        public SupervisorClient(TcpClient client, ITravelerManager travelerCore) : base(client)
        {
            m_travelerManager = travelerCore;
            m_travelers = m_travelerManager.GetTravelers;
            string stationList = "";
            foreach (StationClass station in Traveler.Stations)
            {
                stationList += (stationList.Length != 0 ? "," : "") + '"' + station.Name + '"';
            }
            SendMessage(@"{""stationList"":[" + stationList + "]}");
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
                //StringStream ss = new StringStream(message);
                //Dictionary<string, string> obj = ss.ParseJSON();
                //if (obj.ContainsKey("move") && obj.ContainsKey("destination") && obj.ContainsKey("quantity"))
                //{
                //    //----------------------
                //    // Traveler Completed
                //    //----------------------
                //    for (int i = 0; i < m_travelers.Count; i++)
                //    {
                //        if (m_travelers[i].ID == Convert.ToInt32(obj["move"]))
                //        {
                //            m_travelers[i].Station = Traveler.GetStation(obj["destination"]);
                //            if (m_travelers[i].Station == Traveler.GetStation("Start"))
                //            {
                //                m_travelers[i].Start();
                //            } else
                //            {
                //                m_travelers[i].Advance();
                //            }

                //            // log this event
                //            m_travelers[i].History.Add(new Event(TravelerEvent.Moved, m_travelers[i].Quantity, m_travelers[i].Station));
                //            break;
                //        }
                //    }
                //    TravelersChanged();
                //}
            }
            catch (Exception ex)
            {
                // something went wrong, it is best to just listen for a new message
            }
            ListenAsync();
        }
        public void HandleTravelersChanged(List<Traveler> travelers)
        {
            //string message = @"{""travelers"":[";
            //string travelerJSON = "";
            //foreach (Traveler traveler in m_travelers)
            //{
            //    travelerJSON += (travelerJSON.Length != 0 ? "," : "") + traveler.Export(this.GetType().Name);
            //}
            //message += travelerJSON + "]}";
            //SendMessage(message);
        }
        //------------------------------
        // Private members
        //------------------------------
        //------------------------------
        // Properties
        //------------------------------
        protected ITravelerManager m_travelerManager;
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
