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
            Value = str.Quotate();
        }
        public JsonString(ref StringStream json)
        {
            Value = "";
            char ch = ' ';
            while (json.Get(ref ch)) {
                Value = (Value as string) + ch;
                
                if ((Value as string).Length > 1 && ch == '"') break;
            }
        }
        public override string ToString()
        {
            return (Value as string);
        }
    }
}
