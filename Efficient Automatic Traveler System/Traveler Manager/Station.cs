using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace Efficient_Automatic_Traveler_System
{
    
    public class StationClass : VirtualStation, IEquatable<StationClass>
    {
        #region Public Methods
        public static void ImportStations(string types, string stationsJson)
        {
            m_stations.Clear();
            JsonObject typesObj = (JsonObject)JSON.Parse(types);
            JsonArray stationsObj = (JsonArray)JSON.Parse(stationsJson);
            
            // normal stations
            foreach (JsonObject station in stationsObj)
            {
                JsonObject type = (JsonObject)typesObj[(string)station["type"]];
                m_stations.Add(new StationClass(station, type));
            }
            m_stations.Sort((x, y) => string.Compare(x.Name, y.Name));
            // virtual stations
            VirtualStation.ImportStations(typesObj);
            //ConfigManager.Set("stations", m_stations.Stringify(true, true));
        }
        
        
        public override string ToString()
        {
            JsonObject obj = (JsonObject)JSON.Parse(base.ToString());
            obj.Add("name", m_name);
            obj.Add("mode", m_mode.ToString());
            obj.Add("printers", m_printers.Stringify<string>());
            return obj;
        }
        public bool CreatesThis(Traveler obj)
        {
            return Creates.Exists(x => x == obj.GetType().Name || x == obj.GetType().BaseType.Name);
        }
        public static StationClass GetStation(string name)
        {
            return m_stations.Find(x => x.Name == name);
        }
        public static List<StationClass> GetStations()
        {
            return m_stations;
        }
        public static List<StationClass> StationsInBill(Bill bill)
        {
            List<StationClass> stations = new List<StationClass>();
            foreach (Item componentItem in bill.ComponentItems)
            {
                StationClass station = m_stations.Find(s => s.LaborCodes.Contains(componentItem.ItemCode));
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

        public bool Equals(StationClass other)
        {
            return base.Equals(other);
        }
        public static bool operator ==(StationClass A, StationClass B)
        {
            return (object.ReferenceEquals(A,null) && object.ReferenceEquals(B, null)) 
                || (!object.ReferenceEquals(A, null) && !object.ReferenceEquals(B, null) && A.Name == B.Name);
        }
        public static bool operator !=(StationClass A, StationClass B)
        {
            return !((object.ReferenceEquals(A, null) && object.ReferenceEquals(B, null))
                || (!object.ReferenceEquals(A, null) && !object.ReferenceEquals(B, null) && A.Name == B.Name));
        }
        #endregion
        #region Private Methods
        public StationClass(JsonObject station, JsonObject type) : base(station["type"],type)
        {
            m_name = station["name"];
            m_printers = ((JsonArray)station["printers"]).ToList();
            Enum.TryParse<StationMode>(station["mode"], out m_mode);
        }
        #endregion
        #region Properties
        private int m_ID;
        private string m_name;
        private List<string> m_printers; // list of label printers that this station can/should print to (typicallay a 4x2 and/or a 4x6)
        private StationMode m_mode;

        private static List<StationClass> m_stations = new List<StationClass>();

        #endregion
        #region Interface
        public string Name
        {
            get
            {
                return m_name;
            }
        }
        public StationMode Mode
        {
            get
            {
                return m_mode;
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
