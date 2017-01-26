using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data.Odbc;
using Excel = Microsoft.Office.Interop.Excel;
using Marshal = System.Runtime.InteropServices.Marshal;

namespace Efficient_Automatic_Traveler_System
{
    class Chair : Traveler
    {
        //===========================
        // PUBLIC
        //===========================

        // Doesn't do anything
        public Chair() : base() {}
        // Gets the base properties and orders of the traveler from a json string
        public Chair(string json) : base(json) {
            GetBlacklist();
        }
        // Creates a traveler from a part number and quantity
        public Chair(string partNo, int quantity) : base(partNo, quantity) {
            GetBlacklist();
        }
        // Creates a traveler from a part number and quantity, then loads the bill of materials
        public Chair(string partNo, int quantity, OdbcConnection MAS) : base(partNo, quantity, MAS)
        {
            GetBlacklist();
        }
        //===========================
        // Private
        //===========================
        private void GetBlacklist()
        {
            m_blacklist.Add(new BlacklistItem("/")); // Misc work items
        }
    }
}
