using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Mail;

namespace Efficient_Automatic_Traveler_System
{
    public class ScrapEvent : ProcessEvent//, IForm
    {
        #region Public Methods
        public ScrapEvent(User user, StationClass station, double duration, bool startedWork, string source, string reason) : base(user,station,duration,ProcessType.Scrapped)
        {
            m_startedWork = startedWork;
            m_reason = reason;
            m_source = source;
        }
        public ScrapEvent(string json) : base(json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                m_startedWork = Convert.ToBoolean(obj["startedWork"]);
                m_source = obj["source"];
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
            obj.Add("source", m_source.Quotate());
            obj.Add("reason", m_reason.Quotate());
            return obj.Stringify();
        }
        // IForm
        //public Form CreateForm()
        //{
        //    Form form = new Form();
        //    form.Title = "Scrap";
        //    form.Radio("source","Problem Source", )
        //    form.Selection("station", "Starting Station", StationClass.StationNames());
        //    return form;
        //}

        //public Form CreateFilledForm()
        //{
        //    throw new NotImplementedException();
        //}

        //public void Update(Form form)
        //{
        //    throw new NotImplementedException();
        //}
        #endregion

        //-----------------------------------------------------

        #region Protected Methods
        #endregion

        //-----------------------------------------------------

        #region Properties
        private bool m_startedWork;
        private string m_reason;
        private string m_source;
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

        public string Source
        {
            get
            {
                return m_source;
            }

            set
            {
                m_source = value;
            }
        }
        #endregion
    }
}
