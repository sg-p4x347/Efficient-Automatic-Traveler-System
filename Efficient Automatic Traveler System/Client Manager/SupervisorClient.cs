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
            m_viewState = ItemState.PreProcess;
            SendMessage((new ClientMessage("InitStations", ConfigManager.Get("stations"))).ToString());
            SendMessage((new ClientMessage("InterfaceOpen")).ToString());
            //HandleTravelersChanged(m_travelerManager.GetTravelers);
        }
        public void HandleTravelersChanged(List<Traveler> travelers)
        {
            bool mirror = travelers.Count == m_travelerManager.GetTravelers.Count;
            string message = @"{""travelers"":[";
            string travelerJSON = "";
            foreach (Traveler traveler in travelers.Where(x => x.State == m_viewState || x.Items.Exists(y => y.State == m_viewState)))
            {
                travelerJSON += (travelerJSON.Length > 0 ? "," : "") + traveler.Export(this.GetType().Name, null);
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
        // JS client interface (these are the properties visible to the js interface calling system)
        public ISupervisor TravelerManager
        {
            get
            {
                return m_travelerManager;
            }
        }
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
                returnMessage = new ClientMessage("Info","Error configuring view settings");
            }
            return returnMessage.ToString();
        }
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
