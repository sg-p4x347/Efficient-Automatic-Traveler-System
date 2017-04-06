using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Efficient_Automatic_Traveler_System
{
    enum SummarySort
    {
        Active,
        Available,
        Sorted
    }
    class Summary
    {
        #region Public methods
        public Summary(ITravelerManager travelerManager,string travelerType, SummarySort sortType = SummarySort.Active)
        {
            m_sort = sortType;
            m_travelerType = typeof(Traveler).Assembly.GetType("Efficient_Automatic_Traveler_System." + travelerType);
            switch (m_sort) {
                case SummarySort.Active: m_travelers = travelerManager.GetTravelers.Where(x => x.GetType() == m_travelerType && x.State == ItemState.InProcess && x.Station != StationClass.GetStation("Start")).ToList(); break;
                case SummarySort.Available: m_travelers = travelerManager.GetTravelers.Where(x => x.GetType() == m_travelerType && x.State == ItemState.PreProcess && x.Station == StationClass.GetStation("Start")).ToList(); break;
                case SummarySort.Sorted: m_travelers = travelerManager.GetTravelers.Where(x => x.GetType() == m_travelerType && x.State == ItemState.PreProcess && x.Station != StationClass.GetStation("Start")).ToList(); break;
                default:
                    m_travelers = new List<Traveler>();
                    break;
            }
        }
        /* Creates a summary from two different system states, stored in two sets of files.
         Data is loaded into separate managers for each.
         A summary is then created from each state.
         These two summaries are then compared to yield the final summary which includes
         Delta totals between the time frames.
        */
        public Summary(string pathA, string pathB, string travelerType, SummarySort sortType = SummarySort.Active)
        {
            // starting state
            OrderManager orderManagerA = new OrderManager(pathA);
            orderManagerA.Import();
            TravelerManager travelerManagerA = new TravelerManager(orderManagerA as IOrderManager, pathA);
            travelerManagerA.Import();
            Summary summaryA = new Summary(travelerManagerA as ITravelerManager, travelerType, sortType);
            // ending state
            OrderManager orderManagerB = new OrderManager(pathB);
            orderManagerB.Import();
            TravelerManager travelerManagerB = new TravelerManager(orderManagerB as IOrderManager, pathB);
            travelerManagerB.Import();
            Summary summaryB = new Summary(travelerManagerB as ITravelerManager, travelerType, sortType);

            // Delta state (A's state - B's state)
            m_travelers = (summaryB - summaryA).Travelers;
        }
        public override string ToString()
        {
            List<string> items = new List<string>();
            foreach (Traveler traveler in m_travelers)
            {
                items.Add(traveler.ExportSummary());
            }
            Dictionary<string, string> obj = new Dictionary<string, string>() {
                {"sort", m_sort.ToString().Quotate() },
                {"items",items.Stringify(false) }
            };
            return obj.Stringify();
        }
        public static Summary operator -(Summary B, Summary A)
        {
            foreach (Traveler travelerB in B.Travelers)
            {
                // find the old version of this traveler
                Traveler travelerA = A.Travelers.Find(x => x.ID == travelerB.ID);
                if (travelerA != null)
                {
                    foreach (TravelerItem itemB in travelerB.Items)
                    {
                        // find the old version of this item
                        TravelerItem itemA = travelerA.Items.Find(x => x.ID == itemB.ID);
                        if (itemA != null)
                        {
                            // remove common history
                            itemB.History.RemoveAll(e => e == itemA.History.Find(eA => eA == e));
                        }
                    }
                }
            }
            return B;
        }

        public string MakeCSV()
        {
            string webLocation = "./summary.csv";
            List<string> contents = new List<string>();
            // add the header
            contents.Add((string)m_travelerType.GetMethod("ExportCSVheader").Invoke(null,null));
            // add each detail for each traveler
            foreach (Traveler traveler in m_travelers)
            {
                contents.Add(traveler.ExportCSVdetail());
            }

            File.WriteAllLines(Path.Combine(Server.RootDir, "EATS Client", "summary.csv"), contents.ToArray<string>());

            return webLocation;
        }
        #endregion
        #region Private methods

        #endregion
        #region Properties
        private SummarySort m_sort;
        private Type m_travelerType;
        private List<Traveler> m_travelers;
        #endregion
        #region Interface
        internal List<Traveler> Travelers
        {
            get
            {
                return m_travelers;
            }
        }
        #endregion
    }
}
