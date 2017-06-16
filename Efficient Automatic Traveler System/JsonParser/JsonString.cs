using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    class JsonString : JSON
    {
        public JsonString(string str)
        {
            Value = str;
        }
        public JsonString(ref StringStream json)
        {
            Value = "\"";
            char ch = ' ';
            json.Get(ref ch);
            while (json.Get(ref ch)) {
                Value += ch;
                if (ch == '"') break;
            }
        }
        private string m_value;

        public string Value
        {
            get
            {
                return m_value.DeQuote();
            }

            set
            {
                m_value = value.Quotate();
            }
        }
    }
}
