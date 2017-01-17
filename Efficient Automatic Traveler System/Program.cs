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
                    string message = GetMessage(bytes);
                    //Byte[] test = new Byte[5] { 129, 3,Convert.ToByte('L') , Convert.ToByte('O'), Convert.ToByte('L')};
                    Byte[] test = CreateMessage("TEST!");
                    stream.Write(test, 0, test.Length);
                }
            }
        }
        public Byte[] DecodeMessage(List<Byte> encoded, List<Byte> masks)
        {
            Byte[] decoded = new Byte[encoded.Count];
            for (int i = 0; i < encoded.Count; i++)
            {
                decoded[i] = Convert.ToByte(encoded[i] ^ masks[i % 4]);
            }
            return decoded;
        }
        
        public string GetMessage(Byte[] message)
        {
            string messageString = "";
            List<Byte> masks = new List<Byte>();
            List<Byte> data = new List<Byte>();
            try
            {
                if (message[0] == 129)
                {
                    // this is a self-contained string message
                    if (message[1] >= 128)
                    {
                        int messageLength = message[1] & 127;
                        if (messageLength == 126)
                        {
                            for (int i = 4; i < 8; i++)
                            {
                                masks.Add(message[i]);
                            }
                            for (int i = 8; i < message.Length; i++)
                            {
                                data.Add(message[i]);
                            }
                        }
                        else if (messageLength == 127)
                        {
                            for (int i = 10; i < 14; i++)
                            {
                                masks.Add(message[i]);
                            }
                            for (int i = 14; i < message.Length; i++)
                            {
                                data.Add(message[i]);
                            }
                        }
                        else
                        {
                            for (int i = 2; i < 6; i++)
                            {
                                masks.Add(message[i]);
                            }
                            for (int i = 6; i < message.Length; i++)
                            {
                                data.Add(message[i]);
                            }
                        }
                        messageString = Encoding.UTF8.GetString(DecodeMessage(data, masks));
                    }
                    else
                    {
                        // whoops, this message is not masked :(
                        throw new Exception("Frame is not masked, we have a problem :(");
                    }
                }
            } catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return messageString;
        }
        public Byte[] CreateMessage(string message)
        {
            List<Byte> header = new List<Byte>();
            header.Add(129);
            if (Convert.ToUInt64(message.Length) < 126)
            {
                header.Add(Convert.ToByte(Convert.ToUInt64(message.Length)));
            } else if (Convert.ToUInt64(message.Length) < 65536)
            {
                header.Add(126); // 16 bit length
                header.Add( Convert.ToByte(Convert.ToUInt16(message.Length) >> 8));
                header.Add( Convert.ToByte(Convert.ToUInt16(message.Length)));
            } else
            {
                header.Add(127); // 64 bit length
                header.Add(Convert.ToByte(Convert.ToUInt64(message.Length) >> 56));
                header.Add(Convert.ToByte(Convert.ToUInt64(message.Length) >> 48));
                header.Add(Convert.ToByte(Convert.ToUInt64(message.Length) >> 40));
                header.Add(Convert.ToByte(Convert.ToUInt64(message.Length) >> 32));
                header.Add(Convert.ToByte(Convert.ToUInt64(message.Length) >> 24));
                header.Add(Convert.ToByte(Convert.ToUInt64(message.Length) >> 16));
                header.Add(Convert.ToByte(Convert.ToUInt64(message.Length) >> 8));
                header.Add(Convert.ToByte(Convert.ToUInt64(message.Length)));
            }
            Byte[] headerArray = new Byte[header.Count];
            for (int i = 0; i < header.Count; i++)
            {
                headerArray[i] = header[i];
            }
            Byte[] messageArray = Encoding.UTF8.GetBytes(message);

            Byte[] finalDataArray = new Byte[headerArray.Length + messageArray.Length];
            System.Buffer.BlockCopy(headerArray, 0, finalDataArray, 0, headerArray.Length);
            System.Buffer.BlockCopy(messageArray, 0, finalDataArray, headerArray.Length, messageArray.Length);
            return finalDataArray;
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
