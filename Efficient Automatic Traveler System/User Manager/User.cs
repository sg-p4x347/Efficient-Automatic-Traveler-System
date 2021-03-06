﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    public enum AccessLevel
    {
        Operator = 0,
        Supervisor = 1,
        Administrator = 2
    }
    public class User : IForm, ICSV
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
                Event evtObj = BackupManager.ImportDerived<Event>(evt);
                if (evtObj.Date > DateTime.Today)
                {
                    m_history.Add(evtObj);
                }
            }
            m_mailAddress = obj.ContainsKey("mailAddress") ? obj["mailAddress"] : "";
            Notify = obj.ContainsKey("notify") ? Convert.ToBoolean(obj["notify"]) : false;
        }
        public User(Form form)
        {
            Update(form);
            m_history = new List<Event>();
        }
        public void Update(Form form)
        {
            m_name = form.ValueOf("name");
            m_UID = form.ValueOf("UID");
            m_PWD = form.ValueOf("PWD");
            m_accessLevel = (AccessLevel)Enum.Parse(typeof(AccessLevel), form.ValueOf("accessLevel"));
            m_mailAddress = form.ValueOf("mailAddress");
            Notify = Convert.ToBoolean(form.ValueOf("notify"));
            Server.UserManager.Backup();
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"name",m_name.Quotate() },
                {"UID",m_UID.Quotate() },
                {"PWD",m_PWD.Quotate() },
                {"accessLevel",m_accessLevel.ToString().Quotate() },
                {"history",m_history.Stringify<Event>() },
                {"mailAddress",m_mailAddress.Quotate() },
                {"notify",Notify.ToString().ToLower() }
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
                    m_history.Add(new LogEvent(this, LogType.Login, station: station, client: client.GetType().Name));
                    Server.UserManager.Backup();
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
                m_history.Add(new LogEvent(this, LogType.Logout, logEvents.Last().Station, logEvents.Last().Client));
                Server.UserManager.Backup();
            }
        }

        // returns a json string representing a form to be filled out by a client
        public Form CreateForm()
        {
            Form form = new Form();
            form.Title = "User";
            form.Textbox("name","Name");
            form.Textbox("UID", "User ID");
            form.Textbox("PWD", "Password");
            //form.Selection<AccessLevel>("accessLevel", "Access Level");
            form.Selection("accessLevel", "Access level", ExtensionMethods.GetNamesLessThanOrEqual<AccessLevel>(m_accessLevel));
            form.Textbox("mailAddress", "Mail Address");
            form.Checkbox("notify", "Notifications", false);
            return form;
        }
        public Form CreateFilledForm()
        {
            Form form = new Form();
            form.Title = "Edit User";
            form.Textbox("name", "Name",m_name);
            form.Textbox("UID", "User ID",m_UID);
            form.Textbox("PWD", "Password",m_PWD);
            //form.Selection<AccessLevel>("accessLevel", "Access Level");
            form.Selection("accessLevel", "Access level", ExtensionMethods.GetNamesLessThanOrEqual<AccessLevel>(m_accessLevel),m_accessLevel.ToString());
            form.Textbox("mailAddress", "Mail Address", m_mailAddress);
            form.Checkbox("notify", "Notifications", Notify);
            return form;
        }

        public Dictionary<string, string> ExportCSV(object param)
        {
            List<Traveler> travelers = (List<Traveler>)param;
            Dictionary<string,string> detail = new Dictionary<string, string>() {
                {"Name", m_name},
                {"UID", m_UID },
                {"Access Level", m_accessLevel.ToString() }
            };
            // parts scrapped
            detail.Add("Scrapped parts", travelers.Sum(t => t.Items.Sum(i => i.History.OfType<ScrapEvent>().Where(e => e.User.UID == UID).Count())).ToString());
            // parts completed
            detail.Add("Completed parts", travelers.Sum(t => t.Items.Sum(i => i.History.OfType<ProcessEvent>().Count(e => e.Process == ProcessType.Completed && e.User.UID == UID))).ToString());
            // sum up work minutes at each station
            foreach (StationClass station in StationClass.GetStations())
            {
                double minutes = travelers.Sum(t => t.Items.Sum(i => i.History.OfType<ProcessEvent>().Where(e => e.Station == station && e.User.UID == UID).Sum(h => h.Duration)));
                if (minutes > 0) detail.Add(station.Name + "(min)", minutes.ToString());
            }
            // sum up log minutes
            double totalLogTime = 0.0;
            LogEvent login = null;
            foreach (LogEvent logEvent in History.OfType<LogEvent>())
            {
                if (logEvent.LogType == LogType.Login) {
                    login = logEvent;
                } else if (logEvent.LogType == LogType.Logout && login != null)
                {
                    totalLogTime += (logEvent.Date - login.Date).TotalMinutes;
                }
            }
            detail.Add("Total Log time", totalLogTime.ToString());

            return detail;
        }
        public double TotalLogTime()
        {
            double time = 0.0;
            LogEvent login = null;
            foreach(LogEvent evt in History.OfType<LogEvent>())
            {
                if (login == null && evt.LogType == LogType.Login)
                {
                    login = evt;
                } else if (login != null && evt.LogType == LogType.Logout)
                {
                    
                    time += (evt.Date - login.Date).TotalMinutes;
                    login = null;
                }
            }
            return Math.Round(time, 2);
        }
        #endregion
        #region Properties
        private string m_name;
        private string m_UID;
        private string m_PWD;
        private AccessLevel m_accessLevel;
        private List<Event> m_history;
        private string m_mailAddress;
        private bool m_notify;
        #endregion
        #region Interface

        public string UID
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

        public string Name
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

        public List<Event> History
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

        public string MailAddress
        {
            get
            {
                return m_mailAddress;
            }

            set
            {
                m_mailAddress = value;
            }
        }

        public bool Notify
        {
            get
            {
                return m_notify;
            }

            set
            {
                m_notify = value;
                if (m_notify && m_mailAddress != "")
                {
                    Server.NotificationManager.AddSubscriber(m_mailAddress);
                } else
                {
                    Server.NotificationManager.RemoveSubscriber(m_mailAddress);
                }
            }
        }
        #endregion
    }
}
