using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    class TravelerItem
    {
        public TravelerItem(UInt16 ID)
        {
            m_ID = ID;
            m_scrapped = false;
            m_station = -1;
            m_lastStation = -1;
            m_history = new List<Event>();
            m_order = "";
        }
        public TravelerItem(string json)
        {
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                m_ID = Convert.ToUInt16(obj["ID"]);
                m_scrapped = Convert.ToBoolean(obj["scrapped"]);
                m_station = Convert.ToInt32(obj["station"]);
                m_history = new List<Event>();
                m_order = obj["order"];
                foreach (string eventString in (new StringStream(obj["history"])).ParseJSONarray())
                {
                    m_history.Add(new Event(eventString));
                }
            }
            catch (Exception ex)
            {
                Server.WriteLine("Problem when reading TravelerItem from file: " + ex.Message + "; StackTrace: " + ex.StackTrace);
            }
        }
        public override string ToString()
        {
            string json = "{";
            json += "\"ID\":" + m_ID;
            json += ",\"scrapped\":" + m_scrapped.ToString().ToLower();
            json += ",\"station\":" + m_station;
            json += ",\"lastStation\":" + m_lastStation;
            json += ",\"history\":" + m_history.Stringify<Event>();
            json += ",\"order\":" + m_order.Quotate();
            json += '}';
            return json;
        }
        // Properties
        private UInt16 m_ID;
        private bool m_scrapped;
        private int m_station;
        private int m_lastStation;
        private List<Event> m_history;
        private string m_order;

        public ushort ID
        {
            get
            {
                return m_ID;
            }
        }

        internal int Station
        {
            get
            {
                return m_station;
            }

            set
            {
                m_lastStation = m_station;
                m_station = value;
            }
        }

        public int LastStation
        {
            get
            {
                return m_lastStation;
            }

            set
            {
                m_lastStation = value;
            }
        }

        public bool Scrapped
        {
            get
            {
                return m_scrapped;
            }

            set
            {
                m_scrapped = value;
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

        public string Order
        {
            get
            {
                return m_order;
            }

            set
            {
                m_order = value;
            }
        }

        public bool IsComplete()
        {
            foreach (Event evt in History)
            {
                if (evt.station == Station && evt.type == TravelerEvent.Completed)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
