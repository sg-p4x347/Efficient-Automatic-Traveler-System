using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    public class BlacklistItem
    {
        public BlacklistItem(string itemCode)
        {
            m_itemCode = itemCode;
        }
        public bool StartsWith(string s)
        {
            return s.Substring(0, m_itemCode.Length) == m_itemCode;
        }
        public bool IsEqualTo(string s)
        {
            return s == m_itemCode;
        }
        public bool EndsWith(string s)
        {
            return s.Substring((s.Length - 1) - m_itemCode.Length) == m_itemCode;
        }
        public bool Has(string s)
        {
            return s.IndexOf(m_itemCode) != -1;
        }
        private string m_itemCode = "";

    }
}
