using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    class JsonObject : JSON, IEnumerable<KeyValuePair<string,JSON>>
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
                if (!Char.IsWhiteSpace(ch))
                {
                    if (key == null && ch == '"')
                    {
                        json.PutBack();
                        key = new JsonString(ref json);
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
                        (Value as Dictionary<string, JSON>).Add(key, Import(ref json));
                        key = null;
                    }
                }
            }
        }
        public override string ToString()
        {
            string json = "{";
            bool first = true;
            foreach (KeyValuePair<string, JSON> pair in (Value as Dictionary<string, JSON>) ) {
                if (!first) json += ',';
                json += pair.Key.Quotate() + ':' + pair.Value.ToString();
                first = false;
            }
            json += '}';
            return json;
        }

        public bool ContainsKey(string key)
        {
            return (Value as Dictionary<string, JSON>).ContainsKey(key);
        }
        public void Add(string key, JSON value)
        {
            (Value as Dictionary<string, JSON>).Add(key, value);
        }
        public void Add(string key, object value)
        {
            StringStream stream = new StringStream(value is string ? (value as string).Quotate() : value.ToString());
            Add(key, JSON.Import(ref stream));
        }
        public new JSON this[string key]
        {
            get {
                try
                {
                    return (Value as Dictionary<string, JSON>)[key];
                }
                catch (Exception ex)
                {
                    Server.LogException(ex);
                    Server.WriteLine("JSON Exception");
                    return new JsonNull();
                }
            }
            set
            {
                try
                {
                    (Value as Dictionary<string, JSON>)[key] = value;
                }
                catch (Exception ex)
                {
                    Server.LogException(ex);
                    Server.WriteLine("JSON Exception");
                }
            }
        }

        public IEnumerator<KeyValuePair<string, JSON>> GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, JSON>>)(Value as Dictionary<string, JSON>)).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, JSON>>)(Value as Dictionary<string, JSON>)).GetEnumerator();
        }

        // Properties
        public List<string> Keys
        {
            get
            {
                return (Value as Dictionary<string, JSON>).Keys.ToList();
            }
        }
    }
}
