using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Data.Odbc;
using ExtensionMethods = Efficient_Automatic_Traveler_System.ExtensionMethods;
using System.Net;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Data;

namespace Efficient_Automatic_Traveler_System
{
    public class Server
    {

        //------------------------------
        // Public members
        //------------------------------
        public Server()
        {
            try
            {
                m_online = false;
                m_MAS = new OdbcConnection();
                m_rootDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

                BackupManager.Initialize();
                Configure();
                m_notificationManager = new NotificationManager(ConfigManager.Get("notificationManager"));

                m_orderManager = new OrderManager();
                m_travelerManager = new TravelerManager(m_orderManager as IOrderManager);
                m_clientManager = new ClientManager(m_ip, m_port, m_travelerManager as ITravelerManager);
                // Subscribe events
                m_travelerManager.TravelersChanged += new TravelersChangedSubscriber(m_clientManager.HandleTravelersChanged);

                
                m_clientManagerThread = new Thread(m_clientManager.Start);
                m_clientManagerThread.Name = "Client Manager";

                m_updateInterval = new TimeSpan(24, 0, 0);

                m_userManager = new UserManager();

                // HTTP file serving
            }
            catch (Exception ex)
            {
                WriteLine("Exception constructing server");
                LogException(ex);
            }
        }
        public void Start()
        {
            try
            {
                WriteLine("Server started on " + m_ip + ":80");
                WriteLine("websocket on " + m_ip + ":" + m_port.ToString());

                m_clientManagerThread.Start();

                Update(); // immediately create travelers upon server start
                UpdateTimer(); // start the update loop
                GetInputAsync(); // get console commands from the user
                m_outputLog.Flush();
                KanbanManager.Start();
                Listen(); // start listening for http requests on port 80
                

            }
            catch (Exception ex)
            {
                LogException(ex);
                Server.WriteLine("");
                Server.WriteLine("#################################");
                Server.WriteLine("SERVER RESTARTING FROM HARD CRASH");
                Server.WriteLine("#################################");
                Server.WriteLine("");
                Start();
            }
        }
        public static void WriteLine(string message)
        {
            TextWriter std = Console.Out;
            Console.SetOut(m_outputLog);
            Console.WriteLine(message);
            m_outputLog.Flush();
            Console.SetOut(std);
            Console.WriteLine(message);
        }
        public static void Write(string regex, string message)
        {
            TextWriter std = Console.Out;
            Console.SetOut(m_outputLog);
            Console.Write(regex, message);
            m_outputLog.Flush();
            Console.SetOut(std);
            Console.Write(regex, message);
        }
        public static void LogException(Exception ex)
        {
            Server.WriteLine(new string('!', 100) + Environment.NewLine + "Exception: " + ex.Message + Environment.NewLine + " Stack Trace: " + ex.StackTrace + Environment.NewLine + new string('!', 100) + Environment.NewLine);
        }
        //------------------------------
        // Private members
        //------------------------------
        private async void GetInputAsync()
        {
            string input = await Task.Run(() => Console.ReadLine());
            string[] commands =
            {
                "update",
                "backup",
                "reset",
                "configure",
                "CreateBoxTravelers",
                "relinkOrders",
                "complete",
                "printLabels",
                "delete"
            };
            switch (input)
            {
                case "update":
                    Update();
                    break;
                case "backup":
                    Backup();
                    break;
                case "reset":
                    //m_travelerManager.GetTravelers.Clear();
                    //m_travelerManager.GetOrders.Clear();
                    //m_travelerManager.HandleTravelersChanged();
                    //m_travelerManager.CreateTravelers();
                    break;
                case "configure":
                    Configure();
                    break;
                case "CreateBoxTravelers":
                    List<Box> pre = new List<Box>(TravelerManager.GetTravelers.OfType<Box>());
                    TravelerManager.CreateBoxTravelers();
                    List<Box> post = new List<Box>(TravelerManager.GetTravelers.OfType<Box>());
                    Server.WriteLine("Created " + post.Count(p => !pre.Contains(p)) + " Box travelers");
                    break;
                case "relinkOrders":
                    RelinkOrders();
                    break;
                default:
                    if (input.Contains("complete"))
                    {
                        string[] parts = input.Split(' ');
                        if (parts.Length >= 2)
                        {
                            int travelerID;
                            if (int.TryParse(parts[1], out travelerID))
                            {
                                Traveler traveler = Server.TravelerManager.FindTraveler(travelerID);
                                if (traveler != null)
                                {
                                    for (int i = traveler.QuantityPendingAt(traveler.Station); i > 0; i--)
                                    {
                                        traveler.AddItem(traveler.Station, parts.Length >= 3 ? parts[2] : "");
                                    }
                                    m_travelerManager.Backup();
                                    Server.WriteLine("The deed is done.");
                                    break;
                                }
                            }
                        }
                        Server.WriteLine("Please enter a valid traveler ID");
                    }
                    else if (input.Contains("printLabels"))
                    {
                        // [command] [traveler] [from item id] [to item id] [printer]
                        string[] parts = input.Split(' ');
                        if (parts.Length >= 5)
                        {
                            int travelerID;
                            if (int.TryParse(parts[1], out travelerID))
                            {
                                Traveler traveler = Server.TravelerManager.FindTraveler(travelerID);
                                if (traveler != null)
                                {
                                    ushort fromID;
                                    ushort toID;
                                    if (ushort.TryParse(parts[2], out fromID) && ushort.TryParse(parts[3], out toID))
                                    {
                                        for (ushort i = fromID; i <= toID; i++)
                                        {
                                            TravelerItem item = traveler.FindItem(i);
                                            if (item != null)
                                            {
                                                Thread.Sleep(2000);
                                                traveler.PrintLabel(i, LabelType.Tracking, printer: parts[4]);
                                            }
                                        }
                                    }
                                }
                            }
                            Server.WriteLine("Invalid parameter list");
                        }
                    }
                    else if (input.Contains("delete"))
                    {
                        string[] parts = input.Split(' ');
                        if (parts.Length == 2)
                        {
                            int travelerID;
                            if (int.TryParse(parts[1], out travelerID))
                            {
                                Traveler traveler = Server.TravelerManager.FindTraveler(travelerID);
                                if (traveler != null)
                                {
                                    m_travelerManager.RemoveTraveler(traveler);
                                    m_travelerManager.OnTravelersChanged(traveler);
                                    Server.WriteLine(traveler.PrintID() + " was deleted");
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid; commands are " + commands.ToList().Stringify());

                    }
                    break;
            }
            GetInputAsync();
        }
        //async public void PrintLabels()
        //{
        //    foreach (Traveler traveler in m_travelerCore.Travelers)
        //    {
        //        try
        //        {
        //            string result = "";
        //            using (var client = new WebClient())
        //            {
        //                //client.Credentials = new NetworkCredential("gage", "Stargatep4x347");
        //                client.Headers[HttpRequestHeader.ContentType] = "application/json";
        //                string json = "{\"ID\":\"" + traveler.ID + "\",";
        //                json += "\"Desc1\":\"" + traveler.Part.BillDesc + "\",";
        //                json += "\"Desc2\":\"" + traveler.Eband + "\",";
        //                json += "\"Pack\":\"" + "\",";
        //                //json += "\"Date\":\"" + DateTime.Today.ToString(@"yyyy\-MM\-dd") + "\",";
        //                json += "\"template\":\"" + "4x2 Table Travel1" + "\",";
        //                json += "\"qty\":" + 1 + ",";
        //                json += "\"printer\":\"" + "4x2Pack" + "\"}";

        //                result = client.UploadString(@"http://192.168.2.6:8080/printLabel", "POST", json);
        //                //http://192.168.2.6:8080/printLabel
        //            }
        //        } catch (Exception ex)
        //        {
        //            Console.WriteLine("Label printing exception: " + ex.Message);
        //        }
        //    }
        //}
        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("Local IP Address Not Found!");
        }
        private void UpdateTimer()
        {
            DateTime current = DateTime.Now;
            TimeSpan timeToGo = current.RoundUp(m_updateInterval).TimeOfDay - current.TimeOfDay;
            if (timeToGo.Ticks < 0) timeToGo = timeToGo.Add(new TimeSpan(24, 0, 0));
            m_timer = new System.Threading.Timer(x =>
            {
                Update();
                UpdateTimer();
            }, null, timeToGo, Timeout.InfiniteTimeSpan);
        }
        private void Configure()
        {
            ConfigManager.Import();
            m_port = Convert.ToInt32(ConfigManager.Get("port"));

            // set up the station list
            StationClass.ImportStations(ConfigManager.Get("stationTypes"), ConfigManager.Get("stations"));

            m_ip = GetLocalIPAddress();

            CreateClientConfig();
        }
        public void Update(string orderQuery = "")
        {
            Server.WriteLine("\n<<>><<>><<>><<>><<>> Update <<>><<>><<>><<>><<>>" + DateTime.Now.ToString("\tMM/dd/yyy @ hh:mm") + "\n");
            // Refresh the static managers
            BackupManager.Initialize();
            InventoryManager.Import();
            KanbanManager.Import();

            Configure();

            UserManager.Import();

            // open the MAS connection
            if (ConnectToData())
            {
                UpdateOnline(orderQuery);
            }
            else
            {
                UpdateOffline();
            }

        }
        private void UpdateOnline(string orderQuery = "")
        {
            Server.WriteLine("> Updating in Online mode");
            // Import stored orders from json file and MAS
            m_orderManager.ImportOrders(ref m_MAS);
            m_orderManager.NotifyShipDates();

            // import stored travelers
            m_travelerManager.Import();

            // Remove any finished traveler trees 
            m_travelerManager.CullFinishedTravelers();

            // Import information from MAS
            m_travelerManager.ImportTravelerInfo(m_orderManager as IOrderManager, ref m_MAS);

            // Push planned travelers from the previous day into production
            m_travelerManager.EnterProduction();

            // No more data is needed at this time
            CloseMAS();

            m_orderManager.ReleaseDanglingTravelers();
            // Store current state of data into backup folder
            Backup();
            Server.WriteLine("\n<<>><<>><<>><<>><<>><<>><<>><<>><<>><<>><<>><<>><<>>\n");
        }
        public void CreateTravelers(bool tables = true, bool consolodate = true, bool consolidatePriorityCustomers = true, List<Order> orders = null,Action<double> ReportProgress = null)
        {
            try
            {
                if (ConnectToData())
                {
                    // first, sort the orders by priority customer
                    orders.Sort((a, b) => a.CompareTo(b));
                    // Create, and combine all travelers
                    List<Traveler> newTravelers = m_travelerManager.CompileTravelers(tables, consolodate, consolidatePriorityCustomers, orders);

                    // Finalize the travelers by importing external information
                    m_travelerManager.ImportTravelerInfo(m_orderManager as IOrderManager, ref m_MAS,newTravelers,ReportProgress);

                    m_orderManager.Backup();

                    CloseMAS();
                }
            }
            catch (AccessViolationException ex)
            {
                Server.WriteLine("");
                Server.WriteLine("CAUGHT THE ALMIGHTY ACCESS VIOLATION EXCEPTION!");
                Server.WriteLine("");
                Server.LogException(ex);
                CloseMAS();
            }
        }
        private void UpdateOffline()
        {
            Server.WriteLine("> Updating in Offline mode");
            // Import stored orders from json file and MAS
            m_orderManager.ImportOrders(ref m_MAS);
            // import stored travelers
            m_travelerManager.Import();
            // Push planned travelers from the previous day into production
            m_travelerManager.EnterProduction();

            // No more data is needed at this time
            CloseMAS();

            // Store current state of data into backup folder
            Backup();
            Server.WriteLine("\n<<>><<>><<>><<>><<>><<>><<>><<>><<>><<>><<>><<>><<>>\n");
        }
        // copies memory into a new backup version as insurance
        private void Backup()
        {
            // backup managers' data
            m_travelerManager.Backup();
            m_orderManager.Backup();
            ConfigManager.Backup();
            UserManager.Backup();
        }
        // Opens a connection to the MAS database
        private bool ConnectToData()
        {
            Server.Write("\r{0}", "Connecting to MAS...");
            try
            {
                // initialize the MAS connection
                m_MAS.ConnectionString = "DSN=SOTAMAS90;Company=MGI;";
                m_MAS.ConnectionString = "DSN=SOTAMAS90;Company=MGI;UID=GKC;PWD=sgp4x347;";
                m_MAS.Open();
                if (m_MAS.State == System.Data.ConnectionState.Open)
                {
                    Server.Write("\r{0}", "Connecting to MAS...Connected\n");
                    return true;
                }
                else
                {
                    Server.Write("\r{0}", "Connecting to MAS...Failed\n");
                    return false;
                }

            }
            catch (Exception ex)
            {
                Server.Write("\r{0}", "Connecting to MAS...Failed\n");
                LogException(ex);
                return false;
            }
        }
        public static void HandleODBCexception(Exception ex)
        {
            Server.WriteLine("");
            Server.WriteLine(new string('-', 50));
            Server.WriteLine("An error occured when retrieving information from MAS: " + ex.Message);
            //TimeSpan delay = new TimeSpan(0, 0, 3);
            //Server.WriteLine("Trying again in " + delay.TotalSeconds + " seconds");
            //Server.WriteLine(new string('-', 50));
            //System.Threading.Thread.Sleep(delay);
        }
        public static OdbcConnection GetMasConnection()
        {
            Server.Write("\r{0}", "Connecting to MAS...");
            OdbcConnection mas = new OdbcConnection();
            // initialize the MAS connection
            mas.ConnectionString = "DSN=SOTAMAS90;Company=MGI;";
            mas.ConnectionString = "DSN=SOTAMAS90;Company=MGI;UID=GKC;PWD=sgp4x347;";
            try
            {
                mas.Open();
            }
            catch (Exception ex)
            {
                Server.Write("\r{0}", "Connecting to MAS...Failed\n");
                Server.WriteLine(ex.Message);
            }
            return mas;
        }
        private void CloseMAS()
        {
            m_MAS.Close();
            Server.WriteLine("Disconnected from MAS");
        }
        private void CreateClientConfig()
        {
            StreamWriter config = new StreamWriter(System.IO.Path.Combine(m_rootDirectory, "EATS Client/js/config.js"));
            config.WriteLine("var config = {");
            config.WriteLine("port:" + m_port + ',');
            config.WriteLine("server:\"" + m_ip + "\"");
            // include all public enums
            //var query = Assembly.GetExecutingAssembly()
            //        .GetTypes()
            //        .Where(t => t.IsEnum && t.IsPublic);

            //foreach (Type t in query)
            //{
            //    Console.WriteLine(t);
            //}
            config.WriteLine("};");
            config.Close();
        }

        // Hot fixes
        private void RelinkOrders()
        {
            foreach (Traveler traveler in m_travelerManager.GetTravelers)
            {
                foreach (Order parentOrder in traveler.ParentOrders)
                {
                    foreach (OrderItem item in parentOrder.Items.Where(i => i.ItemCode == traveler.ItemCode))
                    {
                        if (item.ChildTraveler == -1)
                        {
                            // assign this order item to this traveler
                            item.ChildTraveler = traveler.ID;
                            WriteLine("Relinked order: " + parentOrder.SalesOrderNo + " to traveler: " + traveler.PrintID() + " (" + traveler.ItemCode + ")");
                        }
                    }
                }
            }
            Backup();
        }

        // HTTP file serving
        private void Listen()
        {
            m_listener = new HttpListener();

            m_listener.Prefixes.Add("http://" + m_ip + ":80" + "/");
            m_listener.Start();
            while (true)
            {
                try
                {
                    HttpListenerContext context = m_listener.GetContext();
                    Process(context);
                }
                catch (Exception ex)
                {

                }
            }
        }
        private void Process(HttpListenerContext context)
        {
            string filename = context.Request.Url.AbsolutePath;
            filename = filename.Replace("%20", " ");
            //Console.WriteLine(filename);
            filename = filename.Substring(1);
            if (filename.Contains("drawings"))
            {
                filename = Path.Combine(ConfigManager.Get("drawings"), Path.GetFileName(filename));
            }
            else
            {
                if (string.IsNullOrEmpty(filename))
                {
                    foreach (string indexFile in m_indexFiles)
                    {
                        if (File.Exists(Path.Combine(m_rootDirectory, indexFile)))
                        {
                            filename = indexFile;
                            break;
                        }
                    }
                }

                filename = Path.Combine(m_rootDirectory, filename);
            }
            if (File.Exists(filename))
            {
                try
                {
                    Stream input = new FileStream(filename, FileMode.Open);

                    //Adding permanent http response headers
                    string mime;
                    context.Response.ContentType = m_mimeTypeMappings.TryGetValue(Path.GetExtension(filename), out mime) ? mime : "application/octet-stream";
                    context.Response.ContentLength64 = input.Length;
                    context.Response.AddHeader("Date", DateTime.Now.ToString("r"));
                    context.Response.AddHeader("Last-Modified", System.IO.File.GetLastWriteTime(filename).ToString("r"));

                    byte[] buffer = new byte[1024 * 16];
                    int nbytes;
                    while ((nbytes = input.Read(buffer, 0, buffer.Length)) > 0)
                        context.Response.OutputStream.Write(buffer, 0, nbytes);
                    input.Close();

                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.OutputStream.Flush();
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }

            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            }

            context.Response.OutputStream.Close();
        }
        //------------------------------
        // Properties
        //------------------------------
        private static string m_rootDirectory;
        private string m_ip;
        private int m_port;
        private bool m_online;
        private static string m_assembly = "Efficient_Automatic_Traveler_System.";
        private ClientManager m_clientManager;
        private Thread m_clientManagerThread;
        private TimeSpan m_updateInterval;
        private Timer m_timer;
        private static TravelerManager m_travelerManager;
        private static OrderManager m_orderManager;
        private static UserManager m_userManager;
        private static NotificationManager m_notificationManager;
        private OdbcConnection m_MAS;
        private static StreamWriter m_outputLog = new StreamWriter(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "server log.txt"));

        // FILE SERVING---------------------------------------
        private HttpListener m_listener;
        private readonly string[] m_indexFiles = {
            "EATS Client/operator.html",
            "EATS Client/supervisor.html"
        };
        private readonly string[] m_directories =
        {
            @"\\MGFS01\Company\SHARE\common\Drawings\Marco\PDF"
        };
        private static IDictionary<string, string> m_mimeTypeMappings = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) {
            #region extension to MIME type list
            {".asf", "video/x-ms-asf"},
            {".asx", "video/x-ms-asf"},
            {".avi", "video/x-msvideo"},
            {".bin", "application/octet-stream"},
            {".cco", "application/x-cocoa"},
            {".crt", "application/x-x509-ca-cert"},
            {".css", "text/css"},
            {".csv", "text/csv" },
            {".deb", "application/octet-stream"},
            {".der", "application/x-x509-ca-cert"},
            {".dll", "application/octet-stream"},
            {".dmg", "application/octet-stream"},
            {".ear", "application/java-archive"},
            {".eot", "application/octet-stream"},
            {".exe", "application/octet-stream"},
            {".flv", "video/x-flv"},
            {".gif", "image/gif"},
            {".hqx", "application/mac-binhex40"},
            {".htc", "text/x-component"},
            {".htm", "text/html"},
            {".html", "text/html"},
            {".ico", "image/x-icon"},
            {".img", "application/octet-stream"},
            {".iso", "application/octet-stream"},
            {".jar", "application/java-archive"},
            {".jardiff", "application/x-java-archive-diff"},
            {".jng", "image/x-jng"},
            {".jnlp", "application/x-java-jnlp-file"},
            {".jpeg", "image/jpeg"},
            {".jpg", "image/jpeg"},
            {".js", "application/x-javascript"},
            {".mml", "text/mathml"},
            {".mng", "video/x-mng"},
            {".mov", "video/quicktime"},
            {".mp3", "audio/mpeg"},
            {".mpeg", "video/mpeg"},
            {".mpg", "video/mpeg"},
            {".msi", "application/octet-stream"},
            {".msm", "application/octet-stream"},
            {".msp", "application/octet-stream"},
            {".pdb", "application/x-pilot"},
            {".pdf", "application/pdf"},
            {".pem", "application/x-x509-ca-cert"},
            {".pl", "application/x-perl"},
            {".pm", "application/x-perl"},
            {".png", "image/png"},
            {".prc", "application/x-pilot"},
            {".ra", "audio/x-realaudio"},
            {".rar", "application/x-rar-compressed"},
            {".rpm", "application/x-redhat-package-manager"},
            {".rss", "text/xml"},
            {".run", "application/x-makeself"},
            {".sea", "application/x-sea"},
            {".shtml", "text/html"},
            {".sit", "application/x-stuffit"},
            {".swf", "application/x-shockwave-flash"},
            {".tcl", "application/x-tcl"},
            {".tk", "application/x-tcl"},
            {".txt", "text/plain"},
            {".war", "application/java-archive"},
            {".wbmp", "image/vnd.wap.wbmp"},
            {".wmv", "video/x-ms-wmv"},
            {".xml", "text/xml"},
            {".xpi", "application/x-xpinstall"},
            {".zip", "application/zip"},
            #endregion
        };
        public static string RootDir
        {
            get
            {
                return m_rootDirectory;
            }
        }

        public static string Assembly
        {
            get
            {
                return m_assembly;
            }
        }
        public static ITravelerManager TravelerManager
        {
            get
            {
                return m_travelerManager as ITravelerManager;
            }
        }

        public static OrderManager OrderManager
        {
            get
            {
                return m_orderManager;
            }
        }

        public static UserManager UserManager
        {
            get
            {
                return m_userManager;
            }
        }

        public static NotificationManager NotificationManager
        {
            get
            {
                return m_notificationManager;
            }

            set
            {
                m_notificationManager = value;
            }
        }

        public static string RootDirectory
        {
            get
            {
                return m_rootDirectory;
            }

            set
            {
                m_rootDirectory = value;
            }
        }
    }

}
