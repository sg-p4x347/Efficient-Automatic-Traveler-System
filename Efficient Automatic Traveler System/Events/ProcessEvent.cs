using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    enum ProcessType {
        Completed,
        Scrapped
    }

    class ProcessEvent : Event
    {
        #region Public Methods
        public ProcessEvent(User user, StationClass station, double duration, ProcessType process) : base()
        {
            m_user = user;
            m_station = station;
            m_duration = duration;
            m_process = process;
        }
        public ProcessEvent(string json) : base(json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                m_user = UserManager.Find(obj["user"]);
                m_station = StationClass.GetStation(obj["station"]);
                m_duration = Convert.ToDouble(obj["duration"]);
                m_process = (ProcessType)Enum.Parse(typeof(ProcessType), obj["process"]);
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
            }
        }

        public override string ToString()
        {
            Dictionary<string,string> obj = new StringStream( base.ToString()).ParseJSON();
            obj["type"] = this.GetType().ToString().Quotate();
            obj["date"] = obj["date"].Quotate();
            obj.Add("user", m_user.UID.Quotate());
            obj.Add("station", m_station.Name.Quotate());
            obj.Add("duration", m_duration.ToString());
            obj.Add("process", m_process.ToString().Quotate());
            return obj.Stringify();
        }
        #endregion

        //-----------------------------------------------------

        #region Protected Methods
        #endregion

        //-----------------------------------------------------

        #region Properties
        private User m_user;
        private StationClass m_station;
        private double m_duration;
        private ProcessType m_process;
        #endregion

        //-----------------------------------------------------

        #region Interface
        internal User User
        {
            get
            {
                return m_user;
            }

            set
            {
                m_user = value;
            }
        }

        internal StationClass Station
        {
            get
            {
                return m_station;
            }

            set
            {
                m_station = value;
            }
        }

        public double Duration
        {
            get
            {
                return m_duration;
            }

            set
            {
                m_duration = value;
            }
        }

        internal ProcessType Process
        {
            get
            {
                return m_process;
            }

            set
            {
                m_process = value;
            }
        }
        #endregion
    }
}
