using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    enum SummarySort
    {
        Active
    }
    class Summary
    {
        #region Public methods
        public Summary(ITravelerManager travelerManager, SummarySort sortType = SummarySort.Active)
        {
            m_sort = sortType;
            switch (m_sort) {
                case SummarySort.Active:  m_travelers = travelerManager.GetTravelers.Where(x => x.Station != StationClass.GetStation("Start")).ToList(); break;
                default:
                    m_travelers = new List<Traveler>();
                    break;
            }
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
        #endregion
        #region Private methods
        #endregion
        #region Properties
        private SummarySort m_sort;
        private List<Traveler> m_travelers;
        #endregion
        #region Interface
        #endregion
    }
}
