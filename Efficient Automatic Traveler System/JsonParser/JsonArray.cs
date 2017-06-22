using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    class JsonArray : JSON, IEnumerable<JSON>
    {
        public JsonArray()
        {
            Value = new List<JSON>();
        }
        public JsonArray(ref StringStream json)
        {
            Value = new List<JSON>();
            char ch = ']';
            while (json.Get(ref ch))
            {
                if (!Char.IsWhiteSpace(ch))
                {
                    if (ch == ']')
                    {
                        break;
                    }
                    else if (ch == ',') { }
                    else
                    {
                        json.PutBack();
                        (Value as List<JSON>).Add(Import(ref json));
                    }
                }
            }
        }
        public override string ToString()
        {
            string json = "[";
            foreach (JSON element in (Value as List<JSON>))
            {
                json += element.ToString();
            }
            json += ']';
            return json;
        }
        // IEnumerable<JSON>
        public void Add(JSON node)
        {
            (Value as List<JSON>).Add(node);
        }
        public JSON this[int index]
        {
            get { return (Value as List<JSON>)[index]; }
            set { (Value as List<JSON>).Insert(index, value); }
        }

        public IEnumerator<JSON> GetEnumerator()
        {
            return (Value as List<JSON>).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return (Value as List<JSON>).GetEnumerator();
        }
    }
}
