using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Data.Odbc;


namespace Efficient_Automatic_Traveler_System
{
    class Server
    {
        public Server()
        {
            m_ip = "127.0.0.1";
            m_port = 8080;
            m_clientManager = new ClientManager(m_ip, m_port);
            m_clientManagerThread = new Thread(m_clientManager.Start);
        }
        public void Start()
        {
            Console.WriteLine("Server has started on " + m_ip + ":" + m_port.ToString(), Environment.NewLine);
            m_clientManagerThread.Start();
        }
        //------------------------------
        // Properties
        //------------------------------
        private string m_ip;
        private int m_port;
        private ClientManager m_clientManager;
        private Thread m_clientManagerThread;
    }
}
