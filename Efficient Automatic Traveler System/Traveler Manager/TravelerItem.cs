using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    enum ItemState
    {
        PreProcess,
        InProcess,
        PostProcess
    }
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
            m_state = ItemState.PreProcess;
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
                m_state = (ItemState)Enum.Parse(typeof(ItemState),obj["state"]);
            }
            catch (Exception ex)
            {
                Server.WriteLine("Problem when reading TravelerItem from file: " + ex.Message + "; StackTrace: " + ex.StackTrace);
            }
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"ID",m_ID.ToString()},
                {"scrapped", m_scrapped.ToString().ToLower()},
                {"station",Station.ToString() },
                {"lastStation",m_lastStation.ToString() },
                {"history",m_history.Stringify<Event>() },
                {"order",m_order.Quotate() },
                {"state",m_state.ToString().Quotate() }
            };
            return obj.Stringify();
        }
        // Properties
        private UInt16 m_ID;
        private bool m_scrapped;
        private int m_station;
        private int m_lastStation;
        private List<Event> m_history;
        private string m_order;
        private ItemState m_state;

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

        internal ItemState State
        {
            get
            {
                return m_state;
            }

            set
            {
                m_state = value;
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
