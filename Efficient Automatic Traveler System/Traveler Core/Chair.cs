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
        // sorts the table out to its beginning station
        public override void Start()
        {
            SetNextStation();
            Advance();
        }
        // advances this table to the next station
        public override void Advance()
        {
            m_station = m_nextStation;
            SetNextStation();
        }
        //===========================
        // Private
        //===========================

        // returns the next station for this table
        protected override void SetNextStation()
        {
            if (m_station == Traveler.GetStation("Start"))
            {
                m_nextStation = Traveler.GetStation("Chairs");
            }
            else if (m_station == Traveler.GetStation("Chairs"))
            {
                m_nextStation = Traveler.GetStation("Finished");
            }
            else
            {
                m_nextStation = Traveler.GetStation("Start");
            }
        }
        private void GetBlacklist()
        {
            m_blacklist.Add(new BlacklistItem("/")); // Misc work items
        }
    }
}
