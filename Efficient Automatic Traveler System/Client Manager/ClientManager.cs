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
            m_clients = new List<Client>();
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
        public void HandleTravelersChanged()
        {
            foreach (Client client in m_clients)
            {
                client.UpdateTravelers(m_travelers.Where(x => x.ProductionStage == client.ProductionStation).ToList());
            }
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
                Client newClient = new Client(tcpClient,m_clients);
                newClient.Start();
                newClient.UpdateTravelers(m_travelers.Where(x => x.ProductionStage == newClient.ProductionStation).ToList());
                m_clients.Add(newClient);
                Console.WriteLine("A client connected (" + m_clients.Count + " total)");
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
        private List<Client> m_clients;
        private int m_nextClientID;
        private TimeSpan m_updateInterval;
        private Timer m_timer;
        private Func<ProductionStage, List<Traveler>> m_getTravelersAt;
        private List<Traveler> m_travelers;
        //----------
        // Events
        //----------
    }
}
