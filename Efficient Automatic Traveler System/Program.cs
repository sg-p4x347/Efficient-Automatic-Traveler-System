using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace Efficient_Automatic_Traveler_System
{
    
    class Server
    {
        
        public void Start()
        {
            TcpListener server = new TcpListener(IPAddress.Parse("127.0.0.1"), 8080);

            server.Start();
            Console.WriteLine("Server has started on 127.0.0.1:80.{0}Waiting for a connection...", Environment.NewLine);

            TcpClient client = server.AcceptTcpClient();

            Console.WriteLine("A client connected.");

            NetworkStream stream = client.GetStream();

            //enter to an infinite cycle to be able to handle every change in stream
            while (true)
            {
                while (!stream.DataAvailable) ;

                Byte[] bytes = new Byte[client.Available];

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

                    stream.Write(response, 0, response.Length);
                }
                else
                {
                    // send message back

                    //bytes = DecodeMessage(bytes);
                    Byte[] test = new Byte[5] { 129, 3,Convert.ToByte('L') , Convert.ToByte('O'), Convert.ToByte('L')};
                    stream.Write(test, 0, test.Length);
                }
            }
        }
        public Byte[] DecodeMessage(Byte[] encoded)
        {
            Byte[] decoded = new Byte[encoded.Length];
            Byte[] key = new Byte[4] { 61, 84, 35, 6 };

            for (int i = 0; i < encoded.Length; i++)
            {
                decoded[i] = (Byte)(encoded[i] ^ key[i % 4]);
            }
            return decoded;
        }
        
        
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
