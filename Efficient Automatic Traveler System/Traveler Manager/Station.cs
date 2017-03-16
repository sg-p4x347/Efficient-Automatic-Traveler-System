using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace Efficient_Automatic_Traveler_System
{
    enum StationMode
    {
        Batch,
        Serial
    }

    class StationClass
    {
        #region Public Methods
        public StationClass(string json)
        {
            var obj = (new StringStream(json)).ParseJSON();
            m_ID = Convert.ToInt32(obj["ID"]);
            m_name = obj["name"];
            m_canCreateItems = Convert.ToBoolean(obj["canCreateItems"]);
            Enum.TryParse<StationMode>(obj["mode"], out m_mode);
            
            Stations.Add(this);
        }
        public static StationClass FindStation(int ID)
        {
            return Stations.Find(x => x.ID == ID);
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>() {
                { "name", m_name.Quotate()},
                { "ID", m_ID.ToString()},
                { "canCreateItems", m_canCreateItems.ToString().ToLower()},
                { "mode", m_mode.ToString().Quotate()},
            };
            return obj.Stringify();
        }
        public static int GetStation(string key)
        {
            try
            {
                StationClass station = Stations.Find(x => x.Name == key);
                if (station != null) return station.ID;
                // otherwise
                return -1;
            }
            catch (Exception ex)
            {
                return -1;
            }
        }
        public static string GetStationName(int value)
        {
            try
            {
                foreach (StationClass station in Stations)
                {
                    if (station.ID == value)
                    {
                        return station.Name;
                    }
                }
                return "undefined";
            }
            catch (Exception ex)
            {
                return "error";
            }
        }
        #endregion
        #region Private Methods
        #endregion
        #region Properties
        private int m_ID;
        private string m_name;
        private bool m_canCreateItems;
        private StationMode m_mode;

        public static List<StationClass> Stations = new List<StationClass>();

        #endregion
        #region Interface
        public int ID
        {
            get
            {
                return m_ID;
            }

            set
            {
                m_ID = value;
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

        public bool CanCreateItems
        {
            get
            {
                return m_canCreateItems;
            }

            set
            {
                m_canCreateItems = value;
            }
        }

        internal StationMode Mode
        {
            get
            {
                return m_mode;
            }

            set
            {
                m_mode = value;
            }
        }
        #endregion
    }
}
