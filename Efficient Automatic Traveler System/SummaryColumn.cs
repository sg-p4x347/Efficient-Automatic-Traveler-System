using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace Efficient_Automatic_Traveler_System
{
    class SummaryColumn
    {
        public SummaryColumn(string header,string property)
        {
            m_header = header;
            m_property = property;
        }
        public string GetProperty(object obj)
        {
            if (obj == null) { return ""; }
            Type type = obj.GetType();
            PropertyInfo info = type.GetProperty(m_property,BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.Public);
            if (info != null) {
                return info.GetValue(obj, null).ToString();
            }
            return "";
        }
        private string m_header;
        private string m_property;
        public string Header
            {
                get
                {
                    return m_header;
                }
                set
                {
                    m_header = value;
                }
            }

        public string Property
        {
            get
            {
                return m_property;
            }

            set
            {
                m_property = value;
            }
        }
    }
}
