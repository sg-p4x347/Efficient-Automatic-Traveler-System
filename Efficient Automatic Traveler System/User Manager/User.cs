﻿using System;
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
            m_history = new List<Event>();
            foreach (string evt in (new StringStream(obj["history"])).ParseJSONarray())
            {
                m_history.Add(new Event(evt));
            }
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"name",m_name.Quotate() },
                {"UID",m_UID.Quotate() },
                {"history",m_history.Stringify<Event>() }
            };
            return obj.Stringify();
        }
        public void Login(StationClass station)
        {
            m_history.Add(new Event(EventType.Login, 0.0, station));
        }
        public void Logout()
        {
            Event login = m_history.Last<Event>();
            login.time = (DateTime.Now - login.date).TotalMinutes;
        }
        #endregion
        #region Properties
        private string m_name;
        private string m_UID;
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
        #endregion
    }
}
