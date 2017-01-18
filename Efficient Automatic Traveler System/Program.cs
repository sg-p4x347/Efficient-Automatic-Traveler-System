using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

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
    class Program
    {
        static void Main()
        {
            Server server = new Server();
            server.Start();
        }
        //static void Main(string[] args)
        //{

        //    TcpListener serverSocket = new TcpListener(8080);
        //    int requestCount = 0;
        //    TcpClient clientSocket = default(TcpClient);
        //    serverSocket.Start();
        //    Console.WriteLine(" >> Server Started");
        //    clientSocket = serverSocket.AcceptTcpClient();
        //    Console.WriteLine(" >> Accept connection from client");
        //    requestCount = 0;

        //    while ((true))
        //    {
        //        try
        //        {
        //            requestCount = requestCount + 1;
        //            NetworkStream networkStream = clientSocket.GetStream();
        //            byte[] bytesFrom = new byte[10025];
        //            networkStream.Read(bytesFrom, 0, (int)clientSocket.ReceiveBufferSize);
        //            string dataFromClient = System.Text.Encoding.ASCII.GetString(bytesFrom);
        //            dataFromClient = dataFromClient.Substring(0, dataFromClient.IndexOf("$"));
        //            Console.WriteLine(" >> Data from client - " + dataFromClient);
        //            string serverResponse = "Last Message from client" + dataFromClient;
        //            Byte[] sendBytes = Encoding.ASCII.GetBytes(serverResponse);
        //            networkStream.Write(sendBytes, 0, sendBytes.Length);
        //            networkStream.Flush();
        //            Console.WriteLine(" >> " + serverResponse);
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine(ex.ToString());
        //        }
        //    }

        //    clientSocket.Close();
        //    serverSocket.Stop();
        //    Console.WriteLine(" >> exit");
        //    Console.ReadLine();
        //}

    }
}
