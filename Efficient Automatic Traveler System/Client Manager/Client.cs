using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;

using System.Net.Sockets;
using System.Net;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace Efficient_Automatic_Traveler_System
{
    public struct UserAction
    {
        public UserAction(string method, Dictionary<string,object> parameters)
        {
            Method = method;
            Parameters = parameters;
        }
        public string Method;
        public Dictionary<string,object> Parameters;
    }
    public struct ClientMessage
    {
        public ClientMessage(string type, string message)
        {
            Method = type;
            Parameters = (type == "Info" ? message.Quotate() : message);
            CallID = 0;
        }
        public ClientMessage(string type)
        {
            Method = type;
            Parameters = "";
            CallID = 0;
        }
        public override string ToString()
        {
            if (Method != null && Method != "")
            {
                Dictionary<string, string> obj = new Dictionary<string, string>()
                {
                    {"method", Method.Quotate() },
                    {"parameters", (Parameters != "" ? Parameters : "".Quotate())}
                };
                return obj.Stringify();
            } else
            {
                return "";
            }
        }
        public string Method;
        public string Parameters;
        public int CallID;
    }
    /* all derived classes that use ITravelers 
     * implement definitions for thise prototypes */
    interface ITravelers
    {
        event TravelersChangedSubscriber TravelersChanged;
        void HandleTravelersChanged(List<Traveler> travelers);
    }
    // The base class for a TcpClient that connects to the EATS server
    abstract class Client : IClient
    {
        //------------------------------
        // Public members
        //------------------------------
        protected enum MessageTypes
        {
            OperatorUpdate
        }
        public Client(TcpClient client)
        {
            m_TcpClient = client;
            m_stream = m_TcpClient.GetStream();
            m_cts = new CancellationTokenSource();
            m_connected = true;
            m_history = new List<UserAction>();

        }

        public void SendMessage(string message)
        {
            try
            {
                Byte[] test = CreateMessage(message);
                m_stream.Write(test, 0, test.Length);
            } catch (Exception ex)
            {
                // connection was lost
                LostConnection();
            }
        }
        public static async Task<string> RecieveMessageAsync(NetworkStream stream)
        {
            try
            {
                Byte[] bytes = new Byte[1024];
                int length = await stream.ReadAsync(bytes, 0, bytes.Length);
                // Message has arrived, now lets decode it
                return GetMessage(bytes, length);
            }
            catch (Exception ex)
            {
                // connection was lost
                return "connection aborted";
            }
        }
        public void Poll()
        {
            SendMessage("{\"ping\":true}");
        }
        //------------------------------
        // Private members
        //------------------------------

        protected async Task<string> RecieveMessageAsync()
        {
            try
            {
                Byte[] bytes = new Byte[1024];
                int length = await m_stream.ReadAsync(bytes, 0, bytes.Length, m_cts.Token);
                // Message has arrived, now lets decode it
                return GetMessage(bytes, length);
            }
            catch (Exception ex)
            {
                LostConnection();
                return "connection aborted";
            }
        }
        public async void ListenAsync()
        {
            try
            {
                string message = await RecieveMessageAsync();
                if (!Connected) return;
                if (message.Length == 0) {
                    throw new Exception("bad message");
                }
                message = message.Trim('"');
                Dictionary<string, string> obj = (new StringStream(message)).ParseJSON();

                //if (obj.ContainsKey("station"))
                //{
                //    m_station = Convert.ToInt32(obj["station"]);
                //    HandleTravelersChanged(m_travelerManager.GetTravelers);
                //}
                if (obj.ContainsKey("interfaceMethod") && obj.ContainsKey("parameters"))
                {
                    PropertyInfo pi = this.GetType().GetProperty(obj["interfaceTarget"]);
                    if (pi != null)
                    {
                        MethodInfo mi = pi.GetValue(this).GetType().GetMethod(obj["interfaceMethod"]);
                        if (mi != null)
                        {
                            Type attType = typeof(AsyncStateMachineAttribute);
                            // Obtain the custom attribute for the method. 
                            // The value returned contains the StateMachineType property. 
                            // Null is returned if the attribute isn't present for the method. 
                            var attrib = (AsyncStateMachineAttribute)mi.GetCustomAttribute(attType);
                            if (attrib != null)
                            {
                                ListenAsync();
                                // UPDATING... popup
                                SendMessage(new ClientMessage("Updating").ToString());
                                // Await the slow operation
                                ClientMessage reutrnMessage = await (Task<ClientMessage>)(mi.Invoke(this, new object[] { obj["parameters"] }));
                                string messageString = reutrnMessage.ToString();
                                if (messageString != "") SendMessage(messageString);
                            } else
                            {
                                ClientMessage returnMessage = (ClientMessage)(mi.Invoke(this, new object[] { obj["parameters"] }));
                                string messageString = returnMessage.ToString();
                                if (messageString != "") SendMessage(messageString);
                                ListenAsync();
                            }                       
                        } else
                        {
                            ListenAsync();
                        }
                    } else
                    {
                        ListenAsync();
                    }
                } else
                {
                    ListenAsync();
                }
            }
            catch (Exception ex)
            {
                // something went wrong, it is best to just listen for a new message
                ListenAsync();
            }
            
        }
        protected void LostConnection()
        {
            if (m_user != null) m_user.Logout();
            m_cts.Cancel();
            m_connected = false;
        }
        protected static Byte[] DecodeMessage(List<Byte> encoded, List<Byte> masks)
        {
            Byte[] decoded = new Byte[encoded.Count];
            for (int i = 0; i < encoded.Count; i++)
            {
                decoded[i] = Convert.ToByte(encoded[i] ^ masks[i % 4]);
            }
            return decoded;
        }

        protected static string GetMessage(Byte[] message, int length)
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
        protected Byte[] CreateMessage(string message)
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
                header.AddRange(BitConverter.GetBytes(Convert.ToUInt64(message.Length)).Reverse());
                //header.Add(Convert.ToByte(Convert.ToUInt64(message.Length) >> 56));
                //header.Add(Convert.ToByte(Convert.ToUInt64(message.Length) >> 48));
                //header.Add(Convert.ToByte(Convert.ToUInt64(message.Length) >> 40));
                //header.Add(Convert.ToByte(Convert.ToUInt64(message.Length) >> 32));
                //header.Add(Convert.ToByte(Convert.ToUInt64(message.Length) >> 24));
                //header.Add(Convert.ToByte(Convert.ToUInt64(message.Length) >> 16));
                //header.Add(Convert.ToByte(Convert.ToUInt64(message.Length) >> 8));
                //header.Add(Convert.ToByte(message.Length & 0xFF));
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
        protected TcpClient m_TcpClient;
        protected NetworkStream m_stream;
        protected CancellationTokenSource m_cts;
        protected bool m_connected;
        protected User m_user;

        protected List<UserAction> m_history;
        private AccessLevel m_accessLevel;
        public bool Connected
        {
            get
            {
                return m_connected;
            }
        }
        // JS client interface
        public Client This
        {
            get
            {
                return this;
            }
        }

        internal AccessLevel AccessLevel
        {
            get
            {
                return m_accessLevel;
            }

            set
            {
                m_accessLevel = value;
            }
        }

        public virtual ClientMessage Login(string json)
        {
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                // check to see if user exists
                User user = Server.UserManager.Find(obj["UID"]);
                if (user != null)
                {
                    m_user = user;
                    string loginError = m_user.Login(obj["PWD"], this, (obj.ContainsKey("station") ? StationClass.GetStation(obj["station"]) : null));
                    if (loginError == null)
                    {
                        return new ClientMessage("LoginSuccess", user.Name.Quotate());
                    } else
                    {
                        return new ClientMessage("LoginPopup", loginError.Quotate());
                    }

                } else
                {
                    return new ClientMessage("LoginPopup", ("Invalid user ID").Quotate());
                }
                
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
                return new ClientMessage("LoginPopup", ("System error! oops...").Quotate());
            }
        }
        public virtual ClientMessage Logout(string json)
        {
            if (m_user != null)
            {
                m_user.Logout();
                Server.UserManager.Backup();
                m_user = null;
            }
            return new ClientMessage();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void AddHistory(Dictionary<string,object> parameters)
        {
            StackTrace st = new StackTrace();
            StackFrame sf = st.GetFrame(1);
            m_history.Add(new UserAction(sf.GetMethod().Name, parameters));
        }
        // Query the client, callback is determined by result
        public void QueryClient(string condition, string trueCallback, string falseCallback = null)
        {
            string javascript = "if(" + condition + ") {";
            javascript += "new InterfaceCall(" + trueCallback.Quotate('\'') + ");";
            javascript += "} else {";
            if (falseCallback != null)
            {
                javascript += "new InterfaceCall(" + falseCallback.Quotate('\'') + ");";
            }
            javascript += "}";

            SendMessage(new ClientMessage("Evaluate", javascript.Quotate()).ToString());
        }
        //public string AddUID(string json)
        //{
        //    ClientMessage returnMessage = new ClientMessage();
        //    try
        //    {
        //        Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
        //        // check to see if user exists
        //        List<string> users = (new StringStream(ConfigManager.Get("users"))).ParseJSONarray();
        //        users.Add("{\"UID\":" + obj["UID"].Quotate() + "}");
        //        ConfigManager.Set("users", users.Stringify<string>(false, true));
        //    }
        //    catch (Exception ex)
        //    {
        //        Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
        //        returnMessage = new ClientMessage("Info", "error");
        //    }
        //    return returnMessage.ToString();
        //}
    }
}
