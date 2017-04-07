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
            m_travelerManager = travelerManager;
            m_travelers = m_travelerManager.GetTravelers;
            m_viewState = ItemState.PreProcess;
            SendMessage((new ClientMessage("InitStations", ConfigManager.Get("stations"))).ToString());
            SendMessage((new ClientMessage("InitLabelTypes", ExtensionMethods.Stringify<LabelType>())).ToString());
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
        #endregion
        //----------------------------------
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

        public string LoadTraveler(string json)
        {
            return m_travelerManager.LoadTraveler(json);
        }

        public string LoadTravelerAt(string json)
        {
            return m_travelerManager.LoadTravelerAt(json);
        }

        public string LoadItem(string json)
        {
            return m_travelerManager.LoadItem(json);
        }

        public string CreateSummary(string json)
        {
            return m_travelerManager.CreateSummary(json);
        }

        public string DisintegrateTraveler(string json)
        {
            return m_travelerManager.DisintegrateTraveler(json);
        }

        public string EnterProduction(string json)
        {
            return m_travelerManager.EnterProduction(json);
        }

        public string DownloadSummary(string json)
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
                return new ClientMessage("Info",traveler.PrintLabel(Convert.ToUInt16(obj["itemID"]), (LabelType)Enum.Parse(typeof(LabelType), obj["labelType"]),qty > 0 ? qty : 1));
                
            } catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Could not print label(s) due to a pesky error :(");
            }
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
