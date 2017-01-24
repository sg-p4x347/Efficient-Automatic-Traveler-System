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
            m_stream = new FileStream(m_logPath, FileMode.Open, FileAccess.Write, FileShare.Write);
        }
        public void HandleException(string exception)
        {
            m_stream = new FileStream(m_logPath, FileMode.Open, FileAccess.Write, FileShare.Write);
        }
        private string m_logPath;
        private FileStream m_stream;
    }
}
