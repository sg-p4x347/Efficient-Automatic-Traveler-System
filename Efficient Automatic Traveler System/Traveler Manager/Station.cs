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

            // get the types
            m_types = ((JsonObject)ConfigManager.GetJSON("stationTypes")).Keys;
        }
        public static void ConfigureRouting()
        {
            JsonObject types = (JsonObject)ConfigManager.GetJSON("stationTypes");
            foreach (string type in types.Keys)
            {
                List<StationClass> stationsOfType = OfType(type);
                JsonObject typeConfig = (JsonObject)types[type];
                if (typeConfig.ContainsKey("routing"))
                {
                    JsonObject routingTypes = (JsonObject)typeConfig["routing"];
                    foreach (string travelerType in routingTypes.Keys)
                    {
                        JsonObject travelerEntry = (JsonObject)routingTypes[travelerType];
                        JsonArray preRequisites = (JsonArray)travelerEntry["preRequisites"];
                        List<StationClass> preReqs = new List<StationClass>();
                        foreach (string preReqType in preRequisites) preReqs.AddRange(StationClass.OfType(preReqType).Where(s => !preReqs.Contains(s)));
                        foreach (StationClass station in stationsOfType)
                        {
                            if (station.m_preRequisites.ContainsKey(travelerType))
                            {
                                station.m_preRequisites[travelerType].AddRange(preReqs);
                                station.m_preRequisites[travelerType] = station.m_preRequisites[travelerType].Distinct().ToList();
                            } else
                            {
                                station.m_preRequisites.Add(travelerType, preReqs);
                            }
                        }
                    }
                }
            }
        }
        public List<StationClass> PreRequisites(Traveler traveler)
        {
            return m_preRequisites.ContainsKey(traveler.GetType().Name) ? new List<StationClass>(m_preRequisites[traveler.GetType().Name]) : new List<StationClass>();
        }
<<<<<<< HEAD
=======
        
        
>>>>>>> 15b51a0a5c9389ea33a2b2c51e7982bf01c3442e
        public override string ToString()
        {
            JsonObject obj = (JsonObject)JSON.Parse(base.ToString());
            obj.Add("name", m_name.Quotate());
            obj.Add("mode", m_mode.ToString().Quotate());
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
        public static List<StationClass> OfType(string type)
        {
            return m_stations.Where(s => s.Type == type || s.Name == type).ToList();
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
<<<<<<< HEAD
                || (!object.ReferenceEquals(A, null) && !object.ReferenceEquals(B, null) && A.ID == B.ID));
        }
        public bool Is(string name)
        {
            return Name == name;
        }
        public static List<string> StationNames()
        {
            List<string> names = m_stations.Select(s => s.Name).ToList();
            names.Sort((x, y) => x.CompareTo(y));
            return names;
=======
                || (!object.ReferenceEquals(A, null) && !object.ReferenceEquals(B, null) && A.Name == B.Name));
>>>>>>> 15b51a0a5c9389ea33a2b2c51e7982bf01c3442e
        }
        #endregion
        #region Private Methods
        public StationClass(JsonObject station, JsonObject type) : base(station["type"],type)
        {
<<<<<<< HEAD
            var obj = (new StringStream(json)).ParseJSON();
            var typeObj = new StringStream(type).ParseJSON();
            m_ID = StationClass.m_stations.Count;
            m_type = obj["type"];
            m_name = obj["name"];
            m_creates = new StringStream(typeObj["creates"]).ParseJSONarray();
            m_laborCodes = new StringStream(typeObj["laborCodes"]).ParseJSONarray();
            m_printers = new StringStream(obj["printers"]).ParseJSONarray();
            Enum.TryParse<StationMode>(obj["mode"], out m_mode);
            m_preRequisites = new Dictionary<string, List<StationClass>>();
=======
            m_name = station["name"];
            m_printers = ((JsonArray)station["printers"]).ToList();
            Enum.TryParse<StationMode>(station["mode"], out m_mode);
>>>>>>> 15b51a0a5c9389ea33a2b2c51e7982bf01c3442e
        }
        #endregion
        #region Properties
        private int m_ID;
        private string m_name;
        private List<string> m_printers; // list of label printers that this station can/should print to (typicallay a 4x2 and/or a 4x6)
        private StationMode m_mode;
        private Dictionary<string, List<StationClass>> m_preRequisites;
        private static List<StationClass> m_stations = new List<StationClass>();
        private static List<string> m_types = new List<string>();

        #endregion
        #region Interface
<<<<<<< HEAD
        public static List<string> Types
        {
            get
            {
                return m_types;
            }
        }
        public int ID
        {
            get
            {
                return m_ID;
            }
        }

=======
>>>>>>> 15b51a0a5c9389ea33a2b2c51e7982bf01c3442e
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
