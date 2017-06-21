using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    class JsonObject : JSON
    {
        public JsonObject()
        {
            Value = new Dictionary<string, JSON>();
        }
        public JsonObject(ref StringStream json)
        {
            Value = new Dictionary<string, JSON>();
            string key = null;
            char ch = '}';
            while (json.Get(ref ch))
            {
                if (key == null && ch == '"')
                {
                    json.PutBack();
                    key = new JsonString(ref json).Value;
                }
                else if (ch == '}')
                {
                    break;
                }
                else if (ch == ':')
                {
                }
                else if (ch == ',')
                {
                }
                else
                {
                    json.PutBack();
                    Value.Add(key, Import(ref json));
                    key = null;
                }
            }
        }
        private Dictionary<string, JSON> m_value;

        public Dictionary<string, JSON> Value
        {
            get
            {
                return m_value;
            }

            set
            {
                m_value = value;
            }
        }
    }
}
