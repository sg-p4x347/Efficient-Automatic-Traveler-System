using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Timers;

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
    public class ClientStopwatch
    {
        public ClientStopwatch(Client client)
        {
            Client = client;
            Stopwatch = new Stopwatch();
        }
        public ClientStopwatch()
        {
        }
        public void Start(string method = null, double minutes = 0.0)
        {
            Stopwatch.Start();
            CallMethod(method,minutes);
        }
        public void Resume(string method = null)
        {
            Stopwatch.Start();
            CallMethod(method);
        }
        public void CountDown(double minutes, string method)
        {
            Stopwatch.Start();
            CallMethod(method, minutes);
        }
        public void Stop(string method = null)
        {
            Stopwatch.Stop();
            CallMethod(method);
        }
        public void Clear(string method = null)
        {
            Stopwatch.Reset();
            CallMethod(method);
        }
        private void CallMethod(string method = null, double minutes = 0.0)
        {
            if (method != null && Client != null) Client.SendMessage(new ClientMessage(method,minutes.ToString()).ToString());
        }
        public Client Client = null;
        public Stopwatch Stopwatch = new Stopwatch();
    }
    public struct ClientMessage
    {
        public ClientMessage(string method, string message = "",string callback = "")
        {
            Method = method;
            Parameters = (method == "Info" ? message.Quotate() : message);
            Callback = callback;
            CallID = 0;
        }
        public override string ToString()
        {
            if (Method != null && Method != "")
            {
                Dictionary<string, string> obj = new Dictionary<string, string>()
                {
                    {"method", Method.Quotate() },
                    {"parameters", (Parameters != "" ? Parameters : "".Quotate())},
                    {"callback",Callback.Quotate() }
                };
                return obj.Stringify();
            } else
            {
                return "";
            }
        }
        public string Method;
        public string Parameters;
        public string Callback;
        public int CallID;
    }
    /* all derived classes that use ITravelers 
     * implement definitions for thise prototypes */
    public interface ITravelers
    {
        event TravelersChangedSubscriber TravelersChanged;
        void HandleTravelersChanged(bool changed = false);
    }
    // The base class for a TcpClient that connects to the EATS server
    public abstract class Client : IClient
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

            SelectedItem = null;
            SelectedTraveler = null;
            CurrentStation = null;

        }
        public abstract void HandleTravelersChanged(bool changed = false);
        public void SendMessage(string message)
        {
            try
            {
                if (Connected)
                {
                    Byte[] test = CreateMessage(message);
                    m_stream.Write(test, 0, test.Length);
                }
            } catch (Exception ex)
            {
                // connection was lost
                LostConnection();
            }
        }
        public void SendMessage(ClientMessage message)
        {
            SendMessage(message.ToString());
        }

        public void ReportException(Exception ex)
        {
            StackTrace stackTrace = new StackTrace();
            SendMessage(new ClientMessage("Info","Exception in " + this.GetType().Name + "." + stackTrace.GetFrame(1).GetMethod().Name));
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
        protected virtual void UpdateUI()
        {

        }
        //------------------------------
        // Private members
        //------------------------------
        protected NodeList CreateTravelerQueue(List<Traveler> travelers, StationClass station, bool split = true)
        {
            Style visibleOverflow = new Style();
            //visibleOverflow.AddStyle("overflow", "visible");
            NodeList queue = new NodeList(visibleOverflow + new Style("flex-direction-column-reverse"));
            PopulateTravelerQueue(queue, travelers, station, split);
            return queue;
        }
        protected NodeList CreateItemQueue(List<TravelerItem> items)
        {
            Style visibleOverflow = new Style();
            //visibleOverflow.AddStyle("overflow", "visible");
            NodeList queue = new NodeList(visibleOverflow + new Style("flex-direction-column-reverse"));
            PopulateItemQueue(queue, items);
            return queue;
        }
        protected void PopulateTravelerQueue(NodeList queue, List<Traveler> travelers, StationClass station, bool split)
        {
            foreach (Traveler traveler in travelers)
            {
                Row queueItem = CreateTravelerQueueItem(traveler.State, traveler);
                queueItem.EventListeners.Add(new EventListener("click", "LoadTraveler", @"{""travelerID"":" + traveler.ID + "}"));
                Column groupOne = new Column();
                // ID
                string IDtoDisplay = traveler.PrintID();
                if (traveler.ParentTravelers.Any())
                {
                    IDtoDisplay = traveler.GetType().Name.Decompose() + " for ";
                    foreach (Traveler parent in traveler.ParentTravelers)
                    {
                        IDtoDisplay += "<br>";
                        IDtoDisplay += parent.PrintID();
                    }
                }
                groupOne.Add(new TextNode(IDtoDisplay, style: new Style("yellow", "blackOutline")));

                groupOne.Add(new Row()
                {
                    {new TextNode("Total: ",style: new Style("queue__item__qty","white","blackOutline")) },
                    // Total Qty
                    {new TextNode(traveler.Quantity.ToString(),style: new Style("queue__item__qty","lime","blackOutline")) }

                });

                groupOne.Add(new Row(style: new Style("greyBack","stdRadius", "justify-center"))
                {
                    // Qty locally Pending
                    {new TextNode(traveler.QuantityPendingAt(station).ToString(),style: new Style("queue__item__qty","blue","blackOutline")) },
                    // pipe "|"
                    { new TextNode("|",style: new Style("white", "blackOutline")) },
                    // Qty locally InProcess
                    {new TextNode(traveler.QuantityInProcessAt(station).ToString(),style: new Style("queue__item__qty","red","blackOutline")) },
                    // pipe "|"
                    { new TextNode("|",style: new Style("white", "blackOutline")) },
                    // Qty locally PostProcess
                    {new TextNode(traveler.QuantityPostProcessAt(station).ToString(),style: new Style("queue__item__qty","green","blackOutline")) },
                });
                
                queueItem.Add(groupOne);

                Column groupTwo = new Column();
                // ItemCode
                (split ? groupTwo : groupOne).Add(new TextNode(traveler.ItemCode, style: new Style("beige", "blackOutline")));
                // Tables
                if (traveler is Table)
                {
                    // Blanks ready icon
                    //queueItem.Add(new Node(new Style("blanksReady")));
                    // table color
                    (split ? groupTwo : groupOne).Add(new TextNode((traveler as Table).Color, style: new Style("white", "blackOutline")));
                    // Edgebanding color
                    (split ? groupTwo : groupOne).Add(new TextNode((traveler as Table).BandingColor + " EB", style: new Style("white", "blackOutline")));
                }
                if (split) queueItem.Add(groupTwo);
               
                queue.Add(queueItem);
            }
        }
        
        protected void PopulateItemQueue(NodeList queue, List<TravelerItem> items)
        {
            foreach (TravelerItem item in items)
            {
                NodeList queueItem = CreateItemQueueItem(item.GlobalState, item);
                queueItem.ID = item.PrintID();
                queueItem.EventListeners.Add(new EventListener("click", "LoadItem", @"{""travelerID"":" + item.Parent.ID + @",""itemID"":" + item.ID + "}"));
                queueItem.Add(new TextNode(item.Parent.PrintSequenceID(item)));
                queue.Add(queueItem);
            }
        }
        protected virtual Row CreateItemQueueItem(GlobalItemState state, TravelerItem item)
        {
            Row queueItem = CreateQueueItem(state, item.Parent);
            return queueItem;
        }
        protected virtual Row CreateTravelerQueueItem(GlobalItemState state, Traveler traveler)
        {
            Row queueItem = CreateQueueItem(state, traveler);
            return queueItem;
        }
        private Row CreateQueueItem(GlobalItemState state, Traveler traveler)
        {
            Row queueItem = new Row(style: new Style("queue__item", "align-items-center"));
            queueItem.ID = traveler.ID.ToString();
            if (traveler.Quantity <= 0)
            {
                queueItem.Style += new Style("ghostBack");
            }
            if (traveler.ChildTravelers.Exists(child => child.Items.Exists(i => i.Finished)))
            {
                // has at least one finished box item
                queueItem.Style += new Style("purpleBack");
            }
            else
            {
                switch (state)
                {
                    case GlobalItemState.PreProcess: queueItem.Style += new Style("blueBack"); break;
                    case GlobalItemState.InProcess: queueItem.Style += new Style("redBack"); break;
                    case GlobalItemState.Finished: queueItem.Style += new Style("greenBack"); break;
                    default: queueItem.Style += new Style("yellowBack"); break;
                }
            }
            if (traveler is Table)
            {
                queueItem.Style.AddStyle("backgroundImage", "url('./img/" + (traveler as Table).Shape + ".png')");
            } else if (traveler is Box)
            {
                queueItem.Style.AddStyle("backgroundImage", "url('./img/box.png')");
            }
            return queueItem;
        }
        protected async Task<string> RecieveMessageAsync()
        {
            try
            {
                Byte[] bytes = new Byte[128];
                List<Byte> byteList = new List<Byte>();
                int length = 0;
                do
                {
                    length = await m_stream.ReadAsync(bytes, 0, bytes.Length, m_cts.Token);
                    byteList.AddRange(bytes.ToList().GetRange(0, length));
                } while (m_stream.DataAvailable);
                
                //int length = await m_stream.ReadAsync(bytes, 0, bytes.Length, m_cts.Token);
                // Message has arrived, now lets decode it
                return GetMessage(byteList.ToArray(), byteList.Count);
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
                if (message.Length == 0)
                {
                    LostConnection();

                }
                else
                {
                    message = message.Trim('"');
                    Dictionary<string, string> obj = (new StringStream(message)).ParseJSON();

                    if (obj.ContainsKey("interfaceMethod") && obj.ContainsKey("parameters"))
                    {
                        PropertyInfo pi = this.GetType().GetProperty("This");
                        if (pi != null)
                        {
                            MethodInfo mi = pi.GetValue(this).GetType().GetMethod(obj["interfaceMethod"], new[] { typeof(string) });
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
                                    ClientMessage returnMessage = await (Task<ClientMessage>)(mi.Invoke(this, new object[] { obj["parameters"] }));
                                    string messageString = returnMessage.ToString();
                                    if (messageString != "") SendMessage(messageString);
                                }
                                else
                                {
                                    
                                    ClientMessage returnMessage = (ClientMessage)(mi.Invoke(this, new object[] { obj["parameters"] }));
                                    string messageString = returnMessage.ToString();
                                    if (messageString != "") SendMessage(messageString);
                                    ListenAsync();
                                }
                            }
                            else
                            {
                                ListenAsync();
                            }
                        }
                        else
                        {
                            ListenAsync();
                        }
                    }
                    else
                    {
                        ListenAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
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

        private TravelerItem m_selectedItem;
        private TravelerItem m_lastSelectedItem = null;

        private Traveler m_selectedTraveler;
        private Traveler m_lastSelectedTraveler = null;

        private StationClass m_currentStation;

        private static List<CancellationToken> m_cancelTokens;
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

        public AccessLevel AccessLevel
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

        public TravelerItem SelectedItem
        {
            get
            {
                return m_selectedItem;
            }

            set
            {
                m_selectedItem = value;
            }
        }

        public Traveler SelectedTraveler
        {
            get
            {
                return m_selectedTraveler;
            }

            set
            {
                m_selectedTraveler = value;
            }
        }

        public StationClass CurrentStation
        {
            get
            {
                return m_currentStation;
            }

            set
            {
                m_currentStation = value;
            }
        }

        public TravelerItem LastSelectedItem
        {
            get
            {
                return m_lastSelectedItem;
            }

            set
            {
                m_lastSelectedItem = value;
            }
        }

        public Traveler LastSelectedTraveler
        {
            get
            {
                return m_lastSelectedTraveler;
            }

            set
            {
                m_lastSelectedTraveler = value;
            }
        }

        protected static List<CancellationToken> CancelTokens
        {
            get
            {
                return m_cancelTokens;
            }

            set
            {
                m_cancelTokens = value;
            }
        }
        protected static void BeginTask(Task task)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;
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
            Deselect();
            return new ClientMessage();
        }

        public ClientMessage CloseAll(string json = "")
        {
            return new ClientMessage("CloseAll");
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
        protected void Deselect()
        {
            LastSelectedItem = SelectedItem;
            SelectedItem = null;
            LastSelectedTraveler = SelectedTraveler;
            SelectedTraveler = null;
            UpdateUI();
        }
        protected void DeselectItem()
        {
            LastSelectedItem = SelectedItem;
            SelectedItem = null;
            UpdateUI();
        }
        //==============================================
        // Common functionality
        protected Column TravelerView(Traveler traveler, TravelerItem item = null)
        {
            Column travelerView = new Column();
            travelerView.Style.AddStyle("width", "100%");
            travelerView.Style.AddStyle("overflow-x", "hidden");
            // traveler ID
            TextNode travelerID = new TextNode(traveler.PrintSequenceID(item), new Style("yellow"));
            travelerView.Add(travelerID);
            // table
            Node viewTable = ControlPanel.CreateDictionary(traveler.ExportViewProperties());
            viewTable.Style.AddStyle("width", "100%");
            travelerView.Add(viewTable);

            // item table
            if (item != null)
            {
                Dictionary<string, Node> itemProperties = item.ExportViewProperties();
                if (itemProperties.Any())
                {

                    // item table title
                    TextNode itemTitle = new TextNode("Item specific", new Style("yellow"));
                    travelerView.Add(itemTitle);

                    Node itemTable = ControlPanel.CreateDictionary(itemProperties);
                    itemTable.Style.AddStyle("width", "100%");
                    travelerView.Add(itemTable);
                }
            }
            return travelerView;
        }
        protected virtual void SelectItem(TravelerItem item)
        {
            if (SelectedItem != null) DeselectItem();
            SelectedItem = item;
            SelectTraveler(item.Parent);
        }
        protected virtual void SelectTraveler(Traveler traveler)
        {
            if (SelectedTraveler != null)
            {
                LastSelectedTraveler = SelectedTraveler;
                SelectedTraveler = null;
                UpdateUI();
            }
            SelectedTraveler = traveler;
        }
        protected virtual void SearchItem(TravelerItem item)
        {
        }
        protected virtual void SearchTraveler(Traveler traveler)
        {

        }
        public ClientMessage SearchSubmitted(string json)
        {
            try
            {
                if (json.Length > 0)
                {
                    Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                    int travelerID;
                    Traveler traveler;
                    if (obj.ContainsKey("travelerID") && int.TryParse(obj["travelerID"],out travelerID) && Server.TravelerManager.FindTraveler(travelerID,out traveler))
                    {
                        ushort itemID;
                        TravelerItem item;
                        if (obj.ContainsKey("itemID") && ushort.TryParse(obj["itemID"], out itemID) && traveler.FindItem(itemID, out item))
                        {
                            SearchItem(item);
                        }
                        else
                        {
                            SearchTraveler(traveler);
                            //if (itemID > 0) return new ClientMessage("Info", traveler.PrintID() + "-" + obj["itemID"] + " could not be found");
                        }
                    }
                    else
                    {
                        return new ClientMessage("Info", obj["travelerID"] + " does not exist");
                    }
                }
                return new ClientMessage();
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error processing search event");
            }
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


        // Common UI functions
        //public ClientMessage PrintForm(Form form)
        //{
        //    try
        //    {
        //        return ControlPanel.PrintForm(form);
        //    } catch (Exception ex)
        //    {
        //        Server.LogException(ex);
        //        return new ClientMessage("Info", "Error displaying rework details");
        //    }
        //}
        public ClientMessage FlagItemForm(string json)
        {
            try
            {
                if (SelectedItem != null)
                {
                    JsonObject reworkReport = (JsonObject)ConfigManager.GetJSON("scrapReport");
                    JsonArray vendorReasons = (JsonArray)reworkReport["vendor"];
                    JsonArray productionReasons = (JsonArray)reworkReport["production"];

                    Form form = new Form();
                    form.Title = "Flag " + SelectedItem.PrintID();
                    form.Selection("source", "Source", new List<string>() { "vendor", "production" }, "production");
                    form.Selection("reason", "Reason", productionReasons.ToList().Concat(vendorReasons.ToList()).ToList());
                    form.Checkbox("startedWork", "Started Work", false);
                    form.Textbox("comment", "Comment");
                    return form.Dispatch("FlagItem");
                }
                return new ClientMessage("Info", "Selected item was null");
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error loading rework form");
            }
        }
        public ClientMessage ReworkItemForm(string json)
        {
            try
            {
                if (SelectedItem != null)
                {
                    Form form = new Form();
                    form.Title = "Rework " + SelectedItem.PrintID();
                    form.Selection("station", "Station",StationClass.GetStations().Select(s => s.Name).ToList());

                    return form.Dispatch("ReworkItem");
                }
                return new ClientMessage("Info", "Selected item was null");
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error loading rework form");
            }
        }
        public ClientMessage DeflagItemForm(string json)
        {
            try
            {
                return Form.CommentForm("Deflag rework", "reason", "Reason").Dispatch("DeflagItem", json);
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error loading rework form");
            }
        }
        public bool GetItem(string json, out TravelerItem item)
        {
            JsonObject obj = (JsonObject)JSON.Parse(json);
            Traveler traveler = Server.TravelerManager.FindTraveler(obj["travelerID"]);
            if (traveler != null)
            {
                TravelerItem trItem = traveler.FindItem((ushort)obj["itemID"]);
                if (trItem != null)
                {
                    item = trItem;
                    return true;
                } else
                {
                    item = null;
                    return false;
                }
            }
            item = null;
            return false;
        }
        public bool GetStation(string json, out StationClass station)
        {
            try
            {
                JsonObject obj = (JsonObject)JSON.Parse(json);
                station = StationClass.GetStation(obj["station"]);
                return station != null;
            } catch (Exception ex)
            {
                Server.LogException(ex);
                station = null;
                return false;
            }
        }
        public NodeList FlagItemOptions()
        {
           
            string text = "";
            Dictionary<string, string> options = new Dictionary<string, string>();

            // view traveler information

            if (SelectedItem != null)
            {
                if (!SelectedItem.Flagged)
                {
                    // IF not flagged
                    text = "Item not flagged";
                    options.Add("Flag an issue", "FlagItemForm");
                    options.Add("Close", "CloseAll");
                }
                else
                {
                    // IF flagged
                    text = "Item flagged";
                    options.Add("View Details", "ViewFlagDetails");
                    options.Add("Close", "CloseAll");
                }
            }
            return ControlPanel.Options(text, options);
            
        }
        public virtual ClientMessage FlagItem(string json)
        {
            try
            {
                if (SelectedItem != null)
                {
                    SelectedItem.Flag(m_user, new Form(json));
                }
                    
                return new ClientMessage();
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error reworking part");
            }
        }
        public ClientMessage DeflagItem(string json)
        {
            try
            {
                if (SelectedItem != null)
                {
                    SelectedItem.Deflag(m_user, new Form(json));
                }
                return new ClientMessage();
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error cancelling rework status");
            }
        }
        public ClientMessage ViewFlagDetails(string json)
        {
            try
            {
                if (SelectedItem != null)
                {
                    Documentation flagEvent = SelectedItem.History.OfType<Documentation>().Last(e => e.LogType == LogType.FlagItem);
                    Column column = new Column()
                    {
                        ControlPanel.CreateDictionary(flagEvent.ExportViewProperties())
                    };

                    Dictionary<string, string> options = new Dictionary<string, string>();
                    if (this is SupervisorClient) {
                        options.Add("Rework Now", "ReworkItemForm");
                        options.Add("Deflag Item", "DeflagItemForm");
                        options.Add("Scrap Item", "ScrapItem");
                    }
                    
                    column.Add(ControlPanel.Options("Options", options));
                    return new ControlPanel("Flagged details",column).Dispatch();
                }
                return new ClientMessage();
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error loading rework details");
            }

        }
        public ClientMessage ScrapItem(string json)
        {
            try
            {
                if (SelectedItem != null)
                {
                    return new ClientMessage("Info",SelectedItem.Scrap());
                }
                return new ClientMessage("Info","Selected item was null");
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error scrapping item");
            }
        }
        public ClientMessage ReworkItem(string json)
        {
            try
            {
                Form form = new Form(json);
                StationClass station = StationClass.GetStation(form.ValueOf("station"));
                if (SelectedItem != null && station != null)
                {
                    SelectedItem.Rework(m_user, station);
                }
                return new ClientMessage();
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error reworking item");
            }
        }
        public void ReportProgress(double percent,string cancelTask = "")
        {
            if (percent == 1)
            {
                CloseAll();
            } else
            {
                SendMessage(new ClientMessage("Updating", 
                    new JsonObject() {
                        { "text", (Math.Round(percent * 100).ToString() + "%") },
                        { "cancelTask", cancelTask }
                    }));
            }
        }
        //==============================================
    }
}
