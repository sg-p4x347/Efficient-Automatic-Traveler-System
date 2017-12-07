using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    class JsonBool : JSON
    {
        public JsonBool(bool boolean)
        {
            Value = boolean;
        }
        public override string ToString()
        {
            return Value.ToString().ToLower();
        }
    }
}
