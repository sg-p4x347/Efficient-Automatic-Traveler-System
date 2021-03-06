﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    public enum LogType
    {
        Login,
        Logout,
        Finish,
        FlagItem,
        DeflagItem,
        Rework
    }
    public class LogEvent : Event
    {
        #region Public Methods
        public LogEvent(User user, LogType logType, StationClass station = null, string client = "") : base()
        {
            m_user = user;
            m_station = station;
            m_client = client;
            m_logType = logType;
        }
        public LogEvent(string json) : base(json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                m_user = Server.UserManager.Find(obj["user"]);
                m_station = StationClass.GetStation(obj["station"]);
                m_client = obj["client"];
                m_logType = (LogType)Enum.Parse(typeof(LogType), obj["logType"]);
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
            }
        }

        public override string ToString()
        {
            Dictionary<string, string> obj = new StringStream(base.ToString()).ParseJSON(false);
            obj.Add("user", (m_user != null ? m_user.UID : "").Quotate());
            obj.Add("station", (m_station != null ? m_station.Name : "").Quotate());
            obj.Add("client", m_client.Quotate());
            obj.Add("logType", m_logType.ToString().Quotate());
            return obj.Stringify();
        }
        public override string ExportHuman()
        {
            Dictionary<string, string> obj = new StringStream(base.ExportHuman()).ParseJSON(false);
            if (m_user != null) obj.Add("User", m_user.Name.Quotate());
            obj.Add("Station", (m_station != null ? m_station.Name : "").Quotate());
            obj.Add("Client", m_client.Quotate());
            obj.Add("Log type", m_logType.ToString().Quotate());
            return obj.Stringify();
        }
        public override Dictionary<string, Node> ExportViewProperties()
        {
            Dictionary<string, Node> properties = base.ExportViewProperties();
            properties.Add("User", new TextNode(m_user.Name));
            properties.Add("Station", new TextNode(m_station != null ? m_station.Name : ""));
            properties.Add("Log Type", new TextNode(m_logType.ToString()));
            return properties;
        }
        #endregion

        //-----------------------------------------------------

        #region Protected Methods
        #endregion

        //-----------------------------------------------------

        #region Properties
        private User m_user;
        private StationClass m_station;
        private string m_client;
        protected LogType m_logType;


        #endregion

        //-----------------------------------------------------

        #region Interface
        public User User { get
            {
                return m_user;
            }
        }
        public StationClass Station
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

        public LogType LogType
        {
            get
            {
                return m_logType;
            }

            set
            {
                m_logType = value;
            }
        }

        public string Client
        {
            get
            {
                return m_client;
            }

            set
            {
                m_client = value;
            }
        }
        #endregion
    }
}
