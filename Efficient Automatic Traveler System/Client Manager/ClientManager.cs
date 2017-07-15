using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Threading;
using ExtensionMethods = Efficient_Automatic_Traveler_System.ExtensionMethods;

namespace Efficient_Automatic_Traveler_System
{
    class ClientManager
    {
        public ClientManager(string ip, int port, ITravelerManager travelerManager)
        {
            try
            {
                m_server = new TcpListener(IPAddress.Parse(ip), port);
                m_clients = new List<Client>();
                //m_operatorClients = new List<OperatorClient>();
                //m_supervisorClients = new List<SupervisorClient>();
                m_nextClientID = 0;
                
                m_pollInterval = new TimeSpan(0, 0, 3);
                m_travelerManager = travelerManager;
            } catch (Exception ex)
            {
                Server.WriteLine("Failed to create ClientManager: " + ex.Message);
            }
        }
        public void Start()
        {
            Server.WriteLine("Client manager started");
            m_server.Start();
            ConnectAsync();
            Poll();
        }
        public void HandleTravelersChanged(List<Traveler> travelers) {
            for( int i = 0; i < m_clients.Count; i++)
            {
                if (m_clients[i].Connected) {
                    // handle clients that implement ITravelers----------
                    var obj = m_clients[i] as ITravelers;
                    if (obj != null)
                    {
                        obj.HandleTravelersChanged();
                    }
                    //---------------------------------------------------
                }
            }
        }
        public void HandleTravelerChanged(List<Traveler> travelers)
        {
            TravelersChanged(travelers);
        }
        //------------------------------
        // Private
        //------------------------------

        // Poll the clients periodically to test connection
        private void Poll()
        {

            DateTime current = DateTime.Now;
            TimeSpan timeToGo = current.RoundUp(m_pollInterval).TimeOfDay - current.TimeOfDay;
            if (timeToGo.Ticks < 0) timeToGo = timeToGo.Add(new TimeSpan(24, 0, 0));
            m_timer = new System.Threading.Timer(x =>
            {
                for (int i = 0; i < m_clients.Count; i++)
                {
                //m_clients[i].Poll();
                if (!m_clients[i].Connected)
                    {

                    // handle clients that implement ITravelers----------
                    var obj = m_clients[i] as ITravelers;
                        if (obj != null)
                        {
                            obj.TravelersChanged -= HandleTravelersChanged;
                        }
                    //---------------------------------------------------
                    // remove this client
                    m_clients.RemoveAt(i);
                        Console.WriteLine("An operator disconnected (" + m_clients.Count + " total)");
                    }
                }
                Poll();
            }, null, timeToGo, Timeout.InfiniteTimeSpan);
        }
        // Recursive async function that connects and adds websocket clients
        private async void ConnectAsync()
        {
            
            // Wait for a client to connect
            TcpClient tcpClient = await m_server.AcceptTcpClientAsync();
            // a client connected and control resumes here
            if (HandShake(tcpClient))
            {
                //string clientType = await Client.RecieveMessageAsync(tcpClient.GetStream());
                //Type type = typeof(Client).Assembly.GetType("Efficient_Automatic_Traveler_System." + clientType);
                //if (type != null)
                //{
                //    Client client = (Client)Activator.CreateInstance(type, m_travelerManager as ITravelerManager);
                //    client.TravelersChanged += new TravelersChangedSubscriber(HandleTravelerChanged);
                //    client.ListenAsync();
                //    m_clients.Add(client);
                //    Console.WriteLine("A client connected ( " + m_clients.Count + " total )");
                //}
                switch (await Client.RecieveMessageAsync(tcpClient.GetStream()))
                {
                    case "OperatorClient":
                        OperatorClient operatorClient = new OperatorClient(tcpClient, m_travelerManager as ITravelerManager);
                        operatorClient.TravelersChanged += new TravelersChangedSubscriber(HandleTravelerChanged);
                        operatorClient.ListenAsync();
                        m_clients.Add(operatorClient);
                        Console.WriteLine("An operator connected (" + m_clients.Count + " total clients)");
                        break;
                    case "SupervisorClient":
                        SupervisorClient supervisorClient = new SupervisorClient(tcpClient, m_travelerManager as ITravelerManager);
                        supervisorClient.TravelersChanged += new TravelersChangedSubscriber(HandleTravelerChanged);
                        supervisorClient.ListenAsync();
                        m_clients.Add(supervisorClient);
                        Console.WriteLine("A supervisor connected (" + m_clients.Count + " total clients)");
                        break;
                    case "AdministratorClient":
                        AdministratorClient administratorClient = new AdministratorClient(tcpClient);
                        administratorClient.ListenAsync();
                        m_clients.Add(administratorClient);
                        Console.WriteLine("An administrator connected (" + m_clients.Count + " total clients)");
                        break;
                    case "connection aborted": // don't do anything, the connection was lost
                        break;
                }

            }
            ConnectAsync();
        }
        // returns true if the handshake was successful, else false
        private bool HandShake(TcpClient tcpClient)
        {
            NetworkStream stream = tcpClient.GetStream();
            // wait until data is available
            while (!stream.DataAvailable) ;
            Byte[] bytes = new Byte[tcpClient.Available];

            stream.Read(bytes, 0, bytes.Length);

            //translate bytes of request to string
            String data = Encoding.UTF8.GetString(bytes);

            if (new Regex("^GET").IsMatch(data))
            {
                Byte[] response = Encoding.UTF8.GetBytes("HTTP/1.1 101 Switching Protocols" + Environment.NewLine
                    + "Connection: Upgrade" + Environment.NewLine
                    + "Upgrade: websocket" + Environment.NewLine
                    + "Sec-WebSocket-Accept: " + Convert.ToBase64String(
                        SHA1.Create().ComputeHash(
                            Encoding.UTF8.GetBytes(
                                new Regex("Sec-WebSocket-Key: (.*)").Match(data).Groups[1].Value.Trim() + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
                            )
                        )
                    ) + Environment.NewLine
                    + Environment.NewLine);
                // accepted GET request
                stream.Write(response, 0, response.Length);
                return true;
            }
            return false;
        }
        
        //{
        //    DateTime current = DateTime.Now;
        //    TimeSpan timeToGo = current.RoundUp(m_updateInterval).TimeOfDay - current.TimeOfDay;
        //    Server.WriteLine("Will update again in: " + timeToGo.TotalMinutes + " Minutes");
        //    m_timer = new Timer(x =>
        //    {
        //        foreach (Client client in m_clients)
        //        {
        //            client.UpdateTravelers(m_getTravelersAt(client.ProductionStation));
        //        }
        //        Update();
        //    }, null, timeToGo, Timeout.InfiniteTimeSpan);
        //}


        //------------------------------
        // Properties
        //------------------------------
        private TcpListener m_server;
        private List<Client> m_clients;
        //private List<OperatorClient> m_operatorClients;
        //private List<SupervisorClient> m_supervisorClients;
        private int m_nextClientID;
        private Timer m_timer;
        private TimeSpan m_pollInterval;
        private ITravelerManager m_travelerManager;
        //----------
        // Events
        //----------
        public event TravelersChangedSubscriber TravelersChanged;
    }
}
