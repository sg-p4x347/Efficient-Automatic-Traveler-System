using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    enum AccessLevel
    {
        Operator = 0,
        Supervisor = 1,
        Administrator = 2
    }
    class User
    {
        #region Public Methods
        public User(string json)
        {
            Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
            m_name = obj["name"];
            m_UID = obj["UID"];
            m_PWD = obj["PWD"];
            m_accessLevel = (AccessLevel)Enum.Parse(typeof(AccessLevel), obj["accessLevel"]);
            m_history = new List<Event>();
            foreach (string evt in (new StringStream(obj["history"])).ParseJSONarray())
            {
                m_history.Add(BackupManager.ImportDerived<Event>(evt));
            }
        }
        public User(Form form)
        {
            m_name = form.ValueOf("name");
            m_UID = form.ValueOf("UID");
            m_PWD = form.ValueOf("PWD");
            m_accessLevel = (AccessLevel)Enum.Parse(typeof(AccessLevel), form.ValueOf("accessLevel"));
            m_history = new List<Event>();
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"name",m_name.Quotate() },
                {"UID",m_UID.Quotate() },
                {"PWD",m_PWD.Quotate() },
                {"accessLevel",m_accessLevel.ToString().Quotate() },
                {"history",m_history.Stringify<Event>() }
            };
            return obj.Stringify();
        }
        // returns the reason for failure, else null
        public string Login(string PWD, Client client, StationClass station = null)
        {
            if (m_PWD == PWD)
            {
                if (m_accessLevel >= client.AccessLevel)
                {
                    m_history.Add(new LogEvent(this, LogType.Login, station: station, client: typeof(Client).Name));
                } else
                {
                    return "Permission is denied; you must at least be a(n) " + client.AccessLevel.ToString();
                }
            } else
            {
                return "Bad password";
            }
            return null;
        }
        public void Logout(StationClass station = null)
        {
            List<LogEvent> logEvents = m_history.OfType<LogEvent>().ToList();
            if (logEvents.Count > 0 && logEvents.Exists(x => x.LogType == LogType.Login))
            {
                m_history.Add(new LogEvent(this, LogType.Logout, logEvents.Last().Station));
            }
        }

        // returns a json string representing a form to be filled out by a client
        public static string Form()
        {
            Form form = new Form(typeof(User));
            form.Textbox("name","Name");
            form.Textbox("UID", "User ID");
            form.Textbox("PWD", "Password");
            form.Selection<AccessLevel>("accessLevel", "Access Level");
            return form.ToString();
        }
        #endregion
        #region Properties
        private string m_name;
        private string m_UID;
        private string m_PWD;
        private AccessLevel m_accessLevel;
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

        public AccessLevel AccessLevel
        {
            get
            {
                return m_accessLevel;
            }

            set
            {
                m_accessLevel = value;
            }
        }
        #endregion
    }
}
