using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace Efficient_Automatic_Traveler_System
{
    enum EventType
    {
        Completed,
        Scrapped,
        Reworked,
        Moved,
        Login,
        Finished
    }
    abstract class Event : IEquatable<Event>
    {
        #region Public Methods
        public Event(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                m_date = DateTime.ParseExact(obj["date"], "O", CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
            }
        }
        public Event()
        {
            m_date = DateTime.Now;
            m_id = Convert.ToInt32(ConfigManager.Get("nextEventID"));
            ConfigManager.Set("nextEventID", (m_id + 1).ToString());
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"date",m_date.ToString("O").Quotate() }
            };
            obj["type"] = this.GetType().ToString().Quotate();
            return obj.Stringify();
        }
        public virtual string ExportHuman()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"Date",m_date.ToString("MM/dd/yyyy").Quotate() },
                {"Type", this.GetType().Name.Quotate()}
            };
            return obj.Stringify();
        }
        public static bool operator ==(Event A, Event B)
        {
            return (A.m_date == B.m_date);
        }
        public bool Equals(Event B)
        {
            return (m_date == B.m_date);
        }
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }
        public static bool operator !=(Event A, Event B)
        {
            return !(A != null && B != null && A.m_date == B.m_date);
        }
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = m_date.GetHashCode();
                return hashCode;
            }
        }
        #endregion

        //-----------------------------------------------------

        #region Protected Methods
        #endregion

        //-----------------------------------------------------

        #region Properties
        protected DateTime m_date;
        private int m_id;
        #endregion

        //-----------------------------------------------------

        #region Interface
        internal DateTime Date
        {
            get
            {
                return m_date;
            }

            set
            {
                m_date = value;
            }
        }
        #endregion

        //-----------------------------------------------------
    }
}
