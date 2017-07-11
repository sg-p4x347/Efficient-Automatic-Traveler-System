using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    class Documentation : LogEvent
    {
        public Documentation(string json) : base(json)
        {
            JsonObject obj = (JsonObject)JSON.Parse(json);
            if (obj.ContainsKey("data")) Data = obj["data"];
            
        }

        public Documentation(User user, LogType logType, StationClass station = null, JSON data = null) : base(user, logType, station, "")
        {
            Data = data;
        }

        public override string ToString()
        {
            JsonObject obj = (JsonObject)JSON.Parse(base.ToString());
            obj.Add("data",Data != null ?  Data : new JsonObject());
            return obj.ToString();
        }
        public override Dictionary<string, Node> ExportViewProperties()
        {
            Dictionary<string, Node> properties = base.ExportViewProperties();
            properties.Add("Documentation", ControlPanel.PrintForm(new Form(Data)));
            return properties;
        }
        private JSON m_data;

        public JSON Data
        {
            get
            {
                return m_data;
            }

            private set
            {
                m_data = value;
            }
        }
    }
}
