using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    enum LogType
    {
        Login,
        Logout,
        Finish
    }
    class LogEvent : Event
    {
        #region Public Methods
        public LogEvent(User user, StationClass station, LogType logType) : base()
        {
            m_user = user;
            m_station = station;
            m_logType = logType;
        }
        public LogEvent(string json) : base(json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                m_user = UserManager.Find(obj["user"]);
                m_station = StationClass.GetStation(obj["station"]);
                m_logType = (LogType)Enum.Parse(typeof(LogType), obj["logType"]);
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
            }
        }

        public override string ToString()
        {
            Dictionary<string, string> obj = new StringStream(base.ToString()).ParseJSON();
            obj["type"] = this.GetType().ToString().Quotate();
            obj["date"] = obj["date"].Quotate();
            obj.Add("user", m_user != null ? m_user.UID.Quotate() : "".Quotate());
            obj.Add("station", m_station.Name.Quotate());
            obj.Add("logType", m_logType.ToString().Quotate());
            return obj.Stringify();
        }
        #endregion

        //-----------------------------------------------------

        #region Protected Methods
        #endregion

        //-----------------------------------------------------

        #region Properties
        protected User m_user;
        protected StationClass m_station;
        protected LogType m_logType;


        #endregion

        //-----------------------------------------------------

        #region Interface
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

        internal LogType LogType
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
        #endregion
    }
}
