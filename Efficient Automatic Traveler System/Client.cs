using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net.Sockets;
using System.Net;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace Efficient_Automatic_Traveler_System
{
    class Client
    {
        public Client(TcpClient client, List<Client> clients)
        {
            m_clients = clients;
            m_TcpClient = client;
            m_stream = m_TcpClient.GetStream();

            // TEMP
            SendMessage(@"{""ID"":1,""partNo"":""MGTEST-01"",""description"":""this is a very long description that seems to go on and on, without stopping or slowing down; it just keeps going..."",""quantity"":5,""blank"":""MAGRTEST"",""blankQty"":3}");
            SendMessage(@"{""ID"":2,""partNo"":""MGTEST-02"",""description"":""this is a very long description that seems to go on and on, without stopping or slowing down; it just keeps going..."",""quantity"":75,""blank"":""MAGRTEST"",""blankQty"":3}");
            SendMessage(@"{""ID"":3,""partNo"":""MGTEST-03"",""description"":""this is a very long description that seems to go on and on, without stopping or slowing down; it just keeps going..."",""quantity"":0,""blank"":""MAGRTEST"",""blankQty"":3}");
        }
        public async void Start()
        {
            Task<string> messageTask = RecieveMessageAsync();
            string message = await messageTask;

            SendMessage("You (Client #" + m_clients.IndexOf(this).ToString() + ") said: " + message);
            foreach (Client client in m_clients)
            {
                if (client != null && client != this)
                {
                    client.SendMessage("Client " + m_clients.IndexOf(this).ToString() + " said: " + message);
                }
            }
            if (message != "Lost Connection")
            {
                Start();
            }
        }
        public void SendMessage(string message)
        {
            Byte[] test = CreateMessage(message);
            try
            {
                m_stream.Write(test, 0, test.Length);
            } catch (Exception ex)
            {
                // connection was lost
                LostConnection();
            }
        }
        private async Task<string> RecieveMessageAsync()
        {   
            Byte[] bytes = new Byte[1024];
            try
            {
                int length = await m_stream.ReadAsync(bytes, 0, bytes.Length);
                // Message has arrived, now lets decode it
                return GetMessage(bytes, length);
            } catch (Exception ex)
            {
                // connection was lost
                LostConnection();
                return "Lost Connection";
            } 
        }
        private void LostConnection()
        {
            for (int i = 0; i < m_clients.Count; i++)
            {
                if (m_clients[i] != null && m_clients[i] == this)
                {
                    m_clients[i] = null;
                }
            }
        }
        private Byte[] DecodeMessage(List<Byte> encoded, List<Byte> masks)
        {
            Byte[] decoded = new Byte[encoded.Count];
            for (int i = 0; i < encoded.Count; i++)
            {
                decoded[i] = Convert.ToByte(encoded[i] ^ masks[i % 4]);
            }
            return decoded;
        }

        private string GetMessage(Byte[] message,int length)
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
                            for (int i = 8; i < length; i++)
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
                            for (int i = 14; i < length; i++)
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
                            for (int i = 6; i < length; i++)
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
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return messageString;
        }
        private Byte[] CreateMessage(string message)
        {
            List<Byte> header = new List<Byte>();
            header.Add(129);
            if (Convert.ToUInt64(message.Length) < 126)
            {
                header.Add(Convert.ToByte(Convert.ToUInt64(message.Length)));
            }
            else if (Convert.ToUInt64(message.Length) < 65536)
            {
                header.Add(126); // 16 bit length
                header.Add(Convert.ToByte(Convert.ToUInt16(message.Length) >> 8));
                header.Add(Convert.ToByte(message.Length & 0xFF));
            }
            else
            {
                header.Add(127); // 64 bit length
                header.Add(Convert.ToByte(Convert.ToUInt64(message.Length) >> 56));
                header.Add(Convert.ToByte(Convert.ToUInt64(message.Length) >> 48));
                header.Add(Convert.ToByte(Convert.ToUInt64(message.Length) >> 40));
                header.Add(Convert.ToByte(Convert.ToUInt64(message.Length) >> 32));
                header.Add(Convert.ToByte(Convert.ToUInt64(message.Length) >> 24));
                header.Add(Convert.ToByte(Convert.ToUInt64(message.Length) >> 16));
                header.Add(Convert.ToByte(Convert.ToUInt64(message.Length) >> 8));
                header.Add(Convert.ToByte(message.Length & 0xFF));
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
        //------------------------------
        // Properties
        //------------------------------
        private List<Client> m_clients;
        private TcpClient m_TcpClient;
        private NetworkStream m_stream;
    }
}
