using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    public enum StationMode
    {
        Batch,
        Serial
    }

    public class VirtualStation : IEquatable<VirtualStation>
    {
        #region Public Methods
        public static void ImportStations(JsonObject types)
        {
            m_stations.Clear();

            foreach (KeyValuePair<string,JSON> pair in types)
            {
                m_stations.Add(new VirtualStation(pair.Key, (JsonObject)pair.Value));
            }
            m_stations.Sort((x, y) => string.Compare(x.Type, y.Type));
            //ConfigManager.Set("stations", m_stations.Stringify(true, true));
        }

        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>() {
                { "type",m_type.Quotate() },
                { "creates", m_creates.Stringify<string>()},
                { "laborCodes",m_laborCodes.Stringify<string>()}
            };
            return obj.Stringify();
        }
        public bool CreatesThis(Traveler obj)
        {
            return m_creates.Exists(x => x == obj.GetType().Name || x == obj.GetType().BaseType.Name);
        }
        public static VirtualStation GetStation(string type)
        {
            return m_stations.Find(x => x.Type == type);
        }
        public static List<VirtualStation> GetStations()
        {
            return m_stations;
        }
        public static List<VirtualStation> StationsInBill(Bill bill)
        {
            List<VirtualStation> stations = new List<VirtualStation>();
            foreach (Item componentItem in bill.ComponentItems)
            {
                VirtualStation station = m_stations.Find(s => s.LaborCodes.Contains(componentItem.ItemCode));
                if (station != null)
                {
                    stations.Add(station);
                }
            }
            return stations;
        }
        // Equality
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public bool Equals(VirtualStation other)
        {
            return base.Equals(other);
        }
        public bool Equals(object other)
        {
            return base.Equals(other);
        }
        public static bool operator ==(VirtualStation A, VirtualStation B)
        {
            return (object.ReferenceEquals(A, null) && object.ReferenceEquals(B, null))
                || (!object.ReferenceEquals(A, null) && !object.ReferenceEquals(B, null) && A.Type == B.Type);
        }
        public static bool operator !=(VirtualStation A, VirtualStation B)
        {
            return !((object.ReferenceEquals(A, null) && object.ReferenceEquals(B, null))
                || (!object.ReferenceEquals(A, null) && !object.ReferenceEquals(B, null) && A.Type == B.Type));
        }
        public static List<string> StationNames()
        {
            List<string> names = new List<string>();
            foreach (StationClass station in m_stations)
            {
                names.Add(station.Name);
            }
            names.Sort((x, y) => x.CompareTo(y));
            return names;
        }
        #endregion
        #region Private Methods
        public VirtualStation(string type, JsonObject station)
        {
            m_ID = VirtualStation.m_stations.Count;
            m_type = type;
            m_creates = ((JsonArray)station["creates"]).ToList();
            m_laborCodes = ((JsonArray)station["laborCodes"]).ToList();

        }
        #endregion
        #region Properties
        private int m_ID;
        private string m_type;
        private List<string> m_creates; // list of traveler types that this station can create
        private List<string> m_laborCodes; // list of labor codes that are associated with this station

        private static List<VirtualStation> m_stations = new List<VirtualStation>();

        #endregion
        #region Interface
        public int ID
        {
            get
            {
                return m_ID;
            }
        }

        public List<string> Creates
        {
            get
            {
                return m_creates;
            }
        }

        public List<string> LaborCodes
        {
            get
            {
                return m_laborCodes;
            }

            set
            {
                m_laborCodes = value;
            }
        }

        public string Type
        {
            get
            {
                return m_type;
            }

            set
            {
                m_type = value;
            }
        }
        #endregion
    }
}
