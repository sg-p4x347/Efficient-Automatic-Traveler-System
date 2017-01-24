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
            m_updateInterval = new TimeSpan(0, 0, 30);
        }
        public void Start()
        {
            Console.WriteLine("Server has started on " + m_ip + ":" + m_port.ToString(), Environment.NewLine);
            m_clientManagerThread.Start();


            // start the MAS update loop
            Update();
        }
        // Private
        private void Update()
        {
            DateTime current = DateTime.Now;
            TimeSpan timeToGo = RoundUp(current,m_updateInterval).TimeOfDay - current.TimeOfDay;
            Console.WriteLine("Will update again in: " + timeToGo.TotalMinutes + " Minutes");
            m_timer = new System.Threading.Timer(x =>
            {
                GenerateTravelers();
                Update();
            }, null, timeToGo, Timeout.InfiniteTimeSpan);
        }
        private void GenerateTravelers()
        {
            Console.WriteLine("Generating travelers...");
        }
        DateTime RoundUp(DateTime dt, TimeSpan d)
        {
            return new DateTime(((dt.Ticks + d.Ticks - 1) / d.Ticks) * d.Ticks);
        }
        //------------------------------
        // Properties
        //------------------------------
        private string m_ip;
        private int m_port;
        private ClientManager m_clientManager;
        private Thread m_clientManagerThread;
        private TimeSpan m_updateInterval;
        private Timer m_timer;
    }
}
