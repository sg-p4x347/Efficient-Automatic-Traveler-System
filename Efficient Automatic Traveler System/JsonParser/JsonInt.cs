using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    class JsonInt : JSON
    {
        public JsonInt(int integer)
        {
            Value = integer;
        }
        private int m_value;

        public int Value
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
