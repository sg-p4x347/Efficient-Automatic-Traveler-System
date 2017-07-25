using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Mail;

namespace Efficient_Automatic_Traveler_System
{
    public enum ProcessType {
        Started,
        Completed,
        Scrapped
    }

    public class ProcessEvent : Event
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
                m_user = Server.UserManager.Find(obj["user"]);
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
            Dictionary<string,string> obj = new StringStream( base.ToString()).ParseJSON(false);
            obj.Add("user", User != null ? m_user.UID.Quotate() : "");
            obj.Add("station", m_station.Name.Quotate());
            obj.Add("duration", m_duration.ToString());
            obj.Add("process", m_process.ToString().Quotate());
            return obj.Stringify();
        }
        public override string ExportHuman()
        {
            Dictionary<string, string> obj = new StringStream(base.ExportHuman()).ParseJSON(false);
            obj.Add("Process", m_process.ToString().Quotate());
            obj.Add("User", User != null ? m_user.Name.Quotate() : "");
            obj.Add("Station", m_station.Name.Quotate());
            obj.Add("Duration", (Math.Round(m_duration,2).ToString() + " min").Quotate());
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
        public User User
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

        public StationClass Station
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

        public ProcessType Process
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
