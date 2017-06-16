using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    class JsonDouble : JSON
    {
        public JsonDouble(double floating)
        {
            Value = floating;
        }
        private double m_value;

        public double Value
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
