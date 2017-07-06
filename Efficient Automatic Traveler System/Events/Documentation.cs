using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System.Events
{
    class Documentation : LogEvent
    {
        public Documentation(string json) : base(json)
        {
            JsonObject obj = (JsonObject)JSON.Parse(json);
            if (obj.ContainsKey("data")) Data = obj["data"];
            
        }

        public Documentation(User user, LogType logType, StationClass station = null, string client = "") : base(user, logType, station, client)
        {
        }
        public override string ToString()
        {
            JsonObject obj = (JsonObject)JSON.Parse(base.ToString());
            obj.Add("data", Data);
            return obj;
        }
        private JSON m_data;

        public JSON Data
        {
            get
            {
                return m_data;
            }

            set
            {
                m_data = value;
            }
        }
    }
}
