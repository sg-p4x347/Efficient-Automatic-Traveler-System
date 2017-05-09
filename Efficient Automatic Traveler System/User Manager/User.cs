using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    class User
    {
        #region Public Methods
        public User(string json)
        {
            Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
            m_name = obj["name"];
            m_UID = obj["UID"];
            m_PWD = obj["PWD"];
            m_history = new List<Event>();
            foreach (string evt in (new StringStream(obj["history"])).ParseJSONarray())
            {
                m_history.Add(BackupManager.ImportDerived<Event>(evt));
            }
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"name",m_name.Quotate() },
                {"UID",m_UID.Quotate() },
                {"PWD",m_PWD.Quotate() },
                {"history",m_history.Stringify<Event>() }
            };
            return obj.Stringify();
        }
        public bool Login(string PWD, StationClass station = null, string client = null)
        {
            if (m_PWD == PWD)
            {
                m_history.Add(new LogEvent(this, LogType.Login, station: station, client: client));
                return true;
            }
            return false;
        }
        public void Logout(StationClass station = null)
        {
            List<LogEvent> logEvents = m_history.OfType<LogEvent>().ToList();
            if (logEvents.Count > 0 && logEvents.Exists(x => x.LogType == LogType.Login))
            {
                m_history.Add(new LogEvent(this, LogType.Logout, logEvents.Last().Station));
            }
        }
        #endregion
        #region Properties
        private string m_name;
        private string m_UID;
        private string m_PWD;
        private List<Event> m_history;
        #endregion
        #region Interface
        internal string UID
        {
            get
            {
                return m_UID;
            }

            set
            {
                m_UID = value;
            }
        }

        internal string Name
        {
            get
            {
                return m_name;
            }

            set
            {
                m_name = value;
            }
        }

        internal List<Event> History
        {
            get
            {
                return m_history;
            }

            set
            {
                m_history = value;
            }
        }

        public string PWD
        {
            get
            {
                return m_PWD;
            }

            set
            {
                m_PWD = value;
            }
        }
        #endregion
    }
}
