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
        public virtual async void Start()
        {
            IndexJSON(@"{""test"":""{""""}""}");
            string message = await RecieveMessageAsync();
            try
            {
                // handle message ( message type : message )
                MessageTypes type = (MessageTypes)message[0];
                message = message.Remove(0, 1);
                switch (type)
                {
                    case MessageTypes.OperatorUpdate:

                        break;
                }

                m_station = Traveler.GetStation(message);
            }
            catch (Exception ex)
            {
                m_station = 0;
            }
        }
        public void HandleTravelersChanged()
        {
            List<Traveler> stationSpecific = m_travelers.Where(x => x.Station == m_station).ToList();
            string message = @"{""travelers"":[";
            foreach (Traveler traveler in stationSpecific)
            {
                if (traveler.GetType().Name == "Table")
                {
                    message += (message.Length != 0 ? "," : "") + ((Table)traveler).Export(traveler.Station);
                }
                else if (traveler.GetType().Name == "Chair")
                {
                    message += (message.Length != 0 ? "," : "") + ((Chair)traveler).Export();
                }
            }
            message += "]}";
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
    }
}
