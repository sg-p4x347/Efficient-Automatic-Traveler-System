using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Efficient_Automatic_Traveler_System
{
    class ExceptionManager
    {
        public ExceptionManager(string logPath)
        {
            m_logPath = logPath;
        }
        public void HandleException(Exception ex)
        {
            using (StreamWriter writer = new StreamWriter(m_logPath, true))
            {
                writer.WriteLine("Message :" + ex.Message + "<br/>" + Environment.NewLine + "StackTrace :" + ex.StackTrace +
                   "" + Environment.NewLine + "Date :" + DateTime.Now.ToString());
                writer.WriteLine(Environment.NewLine + "-----------------------------------------------------------------------------" + Environment.NewLine);
            }
        }
        private string m_logPath;
    }
}
