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
        private bool m_value;

        public bool Value
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
