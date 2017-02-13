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
        public ClientManager(string ip, int port, ref List<Traveler> travelers)
        {
            m_server = new TcpListener(IPAddress.Parse(ip), port);
            m_operatorClients = new List<OperatorClient>();
            m_supervisorClients = new List<SupervisorClient>();
            m_nextClientID = 0;
            m_updateInterval = new TimeSpan(0, 0, 30);
            m_travelers = travelers;
        }
        public void Start()
        {
            Console.WriteLine("Waiting for a connection...");
            m_server.Start();
            ConnectAsync();
        }
        public void HandleTravelersChanged() {
            for( int i = 0; i < m_operatorClients.Count; i++)
            {
                if (m_operatorClients[i].Connected) {
                    m_operatorClients[i].HandleTravelersChanged();
                } else
                {
                    m_operatorClients[i].TravelersChanged -= HandleTravelersChanged; // unsubscribe from event
                    m_operatorClients.RemoveAt(i);
                }
            }
            for (int i = 0; i < m_supervisorClients.Count; i++)
            {
                if (m_supervisorClients[i].Connected)
                {
                    m_supervisorClients[i].HandleTravelersChanged();
                }
                else
                {
                    m_supervisorClients[i].TravelersChanged -= HandleTravelersChanged; // unsubscribe from event
                    m_supervisorClients.RemoveAt(i);
                }
            }
        }
        public void HandleTravelerChanged()
        {
            TravelersChanged();
        }
        //------------------------------
        // Private
        //------------------------------

        // Recursive async function that connects and adds websocket clients
        private async void ConnectAsync()
        {
            
            // Wait for a client to connect
            TcpClient tcpClient = await m_server.AcceptTcpClientAsync();
            // a client connected and control resumes here
            if (HandShake(tcpClient))
            {
                switch (await Client.RecieveMessageAsync(tcpClient.GetStream()))
                {
                    case "OperatorClient":
                        OperatorClient operatorClient = new OperatorClient(tcpClient, ref m_travelers);
                        operatorClient.TravelersChanged += new TravelersChangedSubscriber(HandleTravelerChanged);
                        operatorClient.ListenAsync();
                        m_operatorClients.Add(operatorClient);
                        Console.WriteLine("An operator connected (" + m_operatorClients.Count + " total)");
                        break;
                    case "SupervisorClient":
                        SupervisorClient supervisorClient = new SupervisorClient(tcpClient, ref m_travelers);
                        supervisorClient.TravelersChanged += new TravelersChangedSubscriber(HandleTravelerChanged);
                        supervisorClient.ListenAsync();
                        m_supervisorClients.Add(supervisorClient);
                        Console.WriteLine("A supervisor connected (" + m_supervisorClients.Count + " total)");
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
        //    Console.WriteLine("Will update again in: " + timeToGo.TotalMinutes + " Minutes");
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
        private List<OperatorClient> m_operatorClients;
        private List<SupervisorClient> m_supervisorClients;
        private int m_nextClientID;
        private TimeSpan m_updateInterval;
        private Timer m_timer;
        private List<Traveler> m_travelers;
        //----------
        // Events
        //----------
        public event TravelersChangedSubscriber TravelersChanged;
    }
}
