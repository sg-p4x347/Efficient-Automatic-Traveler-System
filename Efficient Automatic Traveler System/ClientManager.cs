﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    class ClientManager
    {
        public ClientManager(string ip, int port)
        {
            m_server = new TcpListener(IPAddress.Parse(ip), port);
            m_clients = new List<Client>();
            m_nextClientID = 0;
        }
        public void Start()
        {
            Console.WriteLine("Waiting for a connection...");
            m_server.Start();
            ConnectAsync();
            while (true) ;
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



        //------------------------------
        // Properties
        //------------------------------
        private TcpListener m_server;
        private List<Client> m_clients;
        private int m_nextClientID;
    }
}
