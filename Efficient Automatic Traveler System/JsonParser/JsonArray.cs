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
                        Value.Add(Import(ref json));
                    }
                }
            }
        }
        // IEnumerable<JSON>
        public void Add(JSON node)
        {
            Value.Add(node);
        }
        public JSON this[int index]
        {
            get { return Value[index]; }
            set { Value.Insert(index, value); }
        }

        public IEnumerator<JSON> GetEnumerator()
        {
            return Value.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
        private List<JSON> m_value;

        public List<JSON> Value
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
