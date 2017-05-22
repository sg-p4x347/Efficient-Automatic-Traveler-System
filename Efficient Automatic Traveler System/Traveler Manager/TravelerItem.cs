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
            m_station = StationClass.GetStation("Start");
            m_lastStation = StationClass.GetStation("Start");
            m_history = new List<Event>();
            m_order = "";
            m_state = ItemState.InProcess; // an Item can never be in pre-process; existance implies that it has begun processing
        }
        public TravelerItem(string json)
        {
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                m_ID = Convert.ToUInt16(obj["ID"]);
                m_scrapped = Convert.ToBoolean(obj["scrapped"]);
                m_station = StationClass.GetStation(obj["station"]);
                m_lastStation = StationClass.GetStation(obj["lastStation"]);
                m_history = new List<Event>();
                m_order = obj["order"];
                foreach (string eventString in (new StringStream(obj["history"])).ParseJSONarray())
                {
                    m_history.Add(BackupManager.ImportDerived<Event>(eventString));
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
                {"station",Station.Name.Quotate() },
                {"lastStation",m_lastStation.Name.Quotate() },
                {"history",m_history.ToList<Event>().Stringify<Event>() },
                {"order",m_order.Quotate() },
                {"state",m_state.ToString().Quotate() }
            };
            return obj.Stringify();
        }
        public double ProcessTimeAt(StationClass station)
        {
            return m_history.OfType<ProcessEvent>().ToList().Where(evt => evt.Station == station).Sum(e => e.Duration);
        }
        // Properties
        private UInt16 m_ID;
        private bool m_scrapped;
        private StationClass m_station;
        private StationClass m_lastStation;
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

        internal StationClass Station
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

        public StationClass LastStation
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
            foreach (ProcessEvent evt in History.OfType<ProcessEvent>().ToList())
            {
                if (evt.Station == Station && evt.Process == ProcessType.Completed)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
