﻿using System;
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
        public TravelerItem(UInt16 ID, UInt16 sequenceNo, bool replacement = false)
        {
            m_ID = ID;
            m_sequenceNo = sequenceNo;
            m_replacement = replacement;
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
                m_sequenceNo = obj.ContainsKey("sequenceNo") ? Convert.ToUInt16(obj["sequenceNo"]) : (ushort)0;
                m_replacement = obj.ContainsKey("replacement") ? Convert.ToBoolean(obj["replacement"]) : false;
                m_scrapped = Convert.ToBoolean(obj["scrapped"]);
                m_station = StationClass.GetStation(obj["station"]);
                m_itemCode = obj["itemCode"];
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
                {"sequenceNo",m_sequenceNo.ToString() },
                {"replacement", m_replacement.ToString().ToLower() },
                {"scrapped", m_scrapped.ToString().ToLower()},
                {"station",Station.Name.Quotate() },
                {"itemCode",m_itemCode.Quotate() },
                {"lastStation",m_lastStation.Name.Quotate() },
                {"history",m_history.ToList<Event>().Stringify<Event>() },
                {"order",m_order.Quotate() },
                {"state",m_state.ToString().Quotate() }
            };
            return obj.Stringify();
        }
        public string ExportHuman()
        {
            List<string> history = new List<string>();
            foreach (Event evt in m_history)
            {
                history.Add(evt.ExportHuman());
            }
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"ID",m_ID.ToString()},
                {"Sequence No",m_sequenceNo.ToString() },
                {"Replacement", m_replacement.ToString().ToLower() },
                {"Scrapped", m_scrapped.ToString().ToLower()},
                {"Station",Station.Name.Quotate() },
                {"ItemCode",m_itemCode.Quotate() },
                {"Last station",m_lastStation.Name.Quotate() },
                {"History",history.Stringify(false) },
                {"Order",m_order.Quotate() },
                {"State",m_state.ToString().Quotate() }
            };
            return obj.Stringify();
        }
        public double ProcessTimeAt(StationClass station)
        {
            return m_history.OfType<ProcessEvent>().ToList().Where(evt => evt.Station == station).Sum(e => e.Duration);
        }
        // Properties
        private UInt16 m_ID;
        private UInt16 m_sequenceNo;
        private bool m_replacement;
        private bool m_scrapped;
        private StationClass m_station;
        private string m_itemCode;
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

        public ushort SequenceNo
        {
            get
            {
                return m_sequenceNo;
            }

            set
            {
                m_sequenceNo = value;
            }
        }

        public bool Replacement
        {
            get
            {
                return m_replacement;
            }

            set
            {
                m_replacement = value;
            }
        }

        public string ItemCode
        {
            get
            {
                return m_itemCode;
            }

            set
            {
                m_itemCode = value;
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
