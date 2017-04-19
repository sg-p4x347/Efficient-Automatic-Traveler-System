using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    class ScrapEvent : ProcessEvent
    {
        #region Public Methods
        public ScrapEvent(User user, StationClass station, double duration, bool startedWork, string reason) : base(user,station,duration,ProcessType.Scrapped)
        {
            m_startedWork = startedWork;
            m_reason = reason;
        }
        public ScrapEvent(string json) : base(json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                m_startedWork = Convert.ToBoolean(obj["startedWork"]);
                m_reason = obj["reason"];
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
            }
        }

        public override string ToString()
        {
            Dictionary<string, string> obj = new StringStream(base.ToString()).ParseJSON();
            obj["type"] = this.GetType().ToString().Quotate();
            obj["date"] = obj["date"].Quotate();
            obj["user"] = obj["user"].Quotate();
            obj["station"] = obj["station"].Quotate();
            obj["process"] = obj["process"].Quotate();
            obj.Add("startedWork", m_startedWork.ToString().ToLower());
            obj.Add("reason", m_reason.Quotate());
            return obj.Stringify();
        }
        #endregion

        //-----------------------------------------------------

        #region Protected Methods
        #endregion

        //-----------------------------------------------------

        #region Properties
        private bool m_startedWork;
        private string m_reason;
        #endregion

        //-----------------------------------------------------

        #region Interface
        public bool StartedWork
        {
            get
            {
                return m_startedWork;
            }

            set
            {
                m_startedWork = value;
            }
        }

        public string Reason
        {
            get
            {
                return m_reason;
            }

            set
            {
                m_reason = value;
            }
        }
        #endregion
    }
}
