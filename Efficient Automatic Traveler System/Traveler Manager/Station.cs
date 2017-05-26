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

    internal class StationClass : IEquatable<StationClass>
    {
        #region Public Methods
        public static void ImportStations(string types, string stationsJson)
        {
            m_stations.Clear();
            Dictionary<string, string> stationTypes = new StringStream(types).ParseJSON();
            List <string> stations = new StringStream(stationsJson).ParseJSONarray();
            
            foreach (string stationJSON in stations)
            {
                Dictionary<string, string> obj = new StringStream(stationJSON).ParseJSON();
                m_stations.Add(new StationClass(stationTypes[obj["type"]],stationJSON));
            }
            m_stations.Sort((x, y) => string.Compare(x.Name, y.Name));
            //ConfigManager.Set("stations", m_stations.Stringify(true, true));
        }
        
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>() {
                { "name", m_name.Quotate()},
                { "type",m_type.Quotate() },
                { "creates", m_creates.Stringify<string>()},
                { "mode", m_mode.ToString().Quotate()},
                { "laborCodes",m_laborCodes.Stringify<string>()},
                { "printers",m_printers.Stringify<string>() }
            };
            return obj.Stringify(true);
        }
        public bool CreatesThis(Traveler obj)
        {
            return m_creates.Exists(x => x == obj.GetType().Name || x == obj.GetType().BaseType.Name);
        }
        public static StationClass GetStation(string name)
        {
            return m_stations.Find(x => x.Name == name);
        }
        public static List<StationClass> GetStations()
        {
            return m_stations;
        }
        // Equality
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public bool Equals(StationClass other)
        {
            return base.Equals(other);
        }
        public static bool operator ==(StationClass A, StationClass B)
        {
            return (object.ReferenceEquals(A,null) && object.ReferenceEquals(B, null)) 
                || (!object.ReferenceEquals(A, null) && !object.ReferenceEquals(B, null) && A.ID == B.ID);
        }
        public static bool operator !=(StationClass A, StationClass B)
        {
            return !((object.ReferenceEquals(A, null) && object.ReferenceEquals(B, null))
                || (!object.ReferenceEquals(A, null) && !object.ReferenceEquals(B, null) && A.ID == B.ID));
        }
        public bool Is(string name)
        {
            return Name == name;
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
        private StationClass(string type, string json)
        {
            var obj = (new StringStream(json)).ParseJSON();
            var typeObj = new StringStream(type).ParseJSON();
            m_ID = StationClass.m_stations.Count;
            m_type = obj["type"];
            m_name = obj["name"];
            m_creates = new StringStream(typeObj["creates"]).ParseJSONarray();
            m_laborCodes = new StringStream(typeObj["laborCodes"]).ParseJSONarray();
            m_printers = new StringStream(obj["printers"]).ParseJSONarray();
            Enum.TryParse<StationMode>(obj["mode"], out m_mode);
        }
        #endregion
        #region Properties
        private int m_ID;
        private string m_type;
        private string m_name;
        private List<string> m_creates; // list of traveler types that this station can create
        private List<string> m_laborCodes; // list of labor codes that are associated with this station
        private List<string> m_printers; // list of label printers that this station can/should print to (typicallay a 4x2 and/or a 4x6)
        private StationMode m_mode;

        private static List<StationClass> m_stations = new List<StationClass>();

        #endregion
        #region Interface
        public int ID
        {
            get
            {
                return m_ID;
            }
        }

        public string Name
        {
            get
            {
                return m_name;
            }
        }

        public List<string> Creates
        {
            get
            {
                return m_creates;
            }
        }

        internal StationMode Mode
        {
            get
            {
                return m_mode;
            }
        }

        internal List<string> LaborCodes
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

        public List<string> Printers
        {
            get
            {
                return m_printers;
            }

            set
            {
                m_printers = value;
            }
        }
        #endregion
    }
}
