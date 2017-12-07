using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    public class JsonObject : JSON, IEnumerable<KeyValuePair<string,JSON>>
    {
        public JsonObject()
        {
            Value = new Dictionary<string, JSON>();
        }
        public JsonObject(ref StringStream json)
        {
            
            Value = new Dictionary<string, JSON>();
            try
            {
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
            } catch (Exception ex)
            {

            }
        }
        public static JsonObject From<T1,T2>(Dictionary<T1, T2> dictionary)
        {
            JsonObject obj = new JsonObject();
            foreach (KeyValuePair<T1, T2> pair in dictionary)
            {
                (obj.Value as Dictionary<string, JSON>).Add(pair.Key.ToString(), pair.Value is string ? new JsonString(pair.Value.ToString()) : JSON.Parse(pair.Value.ToString()));
            }
            return obj;
        }
        public override string ToString()
        {
            string json = "{";
            bool first = true;
            foreach (KeyValuePair<string, JSON> pair in (Value as Dictionary<string, JSON>) ) {
                if (!first) json += ',';
                json += pair.Key.Quotate() + ':' + (pair.Value != null ? pair.Value.ToString() :  "null");
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
        public void Add<T>(string key, T value)
        {
            if (value != null)
            {
                string parsedValue = "";
                if (value is string  || (value is Enum && typeof(T).BaseType == typeof(Enum)))
                {
                    parsedValue = value.ToString().Quotate();
                } else
                {
                    parsedValue = value.ToString();
                }
                StringStream stream = new StringStream(parsedValue);
                Add(key, JSON.Import(ref stream));
            } else
            {
                Add(key, new JsonNull());
            }
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
