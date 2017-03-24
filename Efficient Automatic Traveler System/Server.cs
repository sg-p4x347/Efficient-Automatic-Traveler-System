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

namespace Efficient_Automatic_Traveler_System
{
    class Server
    {
        
        //------------------------------
        // Public members
        //------------------------------
        public Server()
        {
            m_MAS = new OdbcConnection();
            m_rootDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            ConfigManager.Open("config.json");
            m_port = Convert.ToInt32(ConfigManager.Get("port"));

            // set up the station list
            List<string> stations = (new StringStream(ConfigManager.Get("stations"))).ParseJSONarray();
            foreach (string json in stations)
            {
                new StationClass(json);
            }

            m_ip = GetLocalIPAddress();

            CreateClientConfig();

            m_orderManager = new OrderManager(m_rootDirectory);
            m_travelerManager = new TravelerManager(m_orderManager as IOrderManager,m_rootDirectory);
            m_clientManager = new ClientManager(m_ip, m_port, m_travelerManager as ITravelerManager);
            // Subscribe events
            m_travelerManager.TravelersChanged += new TravelersChangedSubscriber(m_clientManager.HandleTravelersChanged);

            m_clientManagerThread = new Thread(m_clientManager.Start);
            m_clientManagerThread.Name = "Client Manager";
            m_updateInterval = new TimeSpan(24, 0, 0);
            // HTTP file serving
            
        }
        public void Start()
        {
            try
            {
                Server.WriteLine("Server started on " + m_ip + ":80"); 
                Server.WriteLine("websocket on " + m_ip + ":" + m_port.ToString());
                m_clientManagerThread.Start();

                
                Update(); // immediately create travelers upon server start
                UpdateTimer(); // start the update loop
                GetInputAsync(); // get console commands from the user
                m_outputLog.Flush();
                
                Listen(); // start listening for http requests on port 80
            }
            catch (Exception ex)
            {
                Server.WriteLine(new string('!', 100) + Environment.NewLine + "Exception: " + ex.Message + Environment.NewLine + " Stack Trace: " + ex.StackTrace + Environment.NewLine + new string('!', 100) + Environment.NewLine);
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
            Console.Write(regex,message);
            m_outputLog.Flush();
            Console.SetOut(std);
            Console.Write(regex,message);
        }
        //------------------------------
        // Private members
        //------------------------------
        private async void GetInputAsync()
        {
            string input = await Task.Run(() => Console.ReadLine());
            switch (input)
            {
                case "update":
                    Update();
                    break;
                case "reset":
                    //m_travelerManager.GetTravelers.Clear();
                    //m_travelerManager.GetOrders.Clear();
                    //m_travelerManager.HandleTravelersChanged();
                    //m_travelerManager.CreateTravelers();
                    break;
                default:
                    Console.WriteLine("Invalid; commands are [update, reset]");
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
            if (timeToGo.Ticks < 0) timeToGo = timeToGo.Add(new TimeSpan(24,0,0));
            m_timer = new System.Threading.Timer(x =>
            {
                Update();
                UpdateTimer();
            }, null, timeToGo, Timeout.InfiniteTimeSpan);
        }
        private void Update()
        {
            Server.WriteLine("\n<<>><<>><<>><<>><<>> Update <<>><<>><<>><<>><<>>" + DateTime.Now.ToString("\tMM/dd/yyy @ hh:mm") + "\n");
            
            // Store current state of data into backup folder
            Backup();

            // open the MAS connection
            ConnectToData();

            // Import stored orders from json file and MAS
            m_orderManager.ImportOrders(ref m_MAS);

            // Load, Create, and combine all travelers
            m_travelerManager.CompileTravelers();

            // backup everything
            m_orderManager.BackupOrders();
            m_travelerManager.BackupTravelers();

            // compensate order items for inventory balances
            m_orderManager.CheckInventory(m_travelerManager as ITravelerManager, ref m_MAS);

            // Finalize the travelers by importing external information
            m_travelerManager.ImportTravelerInfo(m_orderManager as IOrderManager, ref m_MAS);
            
            // No more data is needed at this time
            CloseMAS();

            
            Server.WriteLine("\n<<>><<>><<>><<>><<>><<>><<>><<>><<>><<>><<>><<>>\n");
        }
        // copies memory into a new backup version as insurance
        private void Backup()
        {
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            // remove backups older than set time frame
            int maxAge = -10; // in days
            string[] backupFolders = System.IO.Directory.GetDirectories(System.IO.Path.Combine(exeDir, "backup\\"));
            foreach (string folder in backupFolders)
            {
                if (DateTime.Parse(new DirectoryInfo(folder).Name) < DateTime.Today.AddDays(maxAge))
                {
                    System.IO.Directory.Delete(folder, true);
                }
            }
            // backup folder for today
            string folderName = DateTime.Today.ToString("MM-dd-yyyy");
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(exeDir, "backup\\" + folderName));
            // backup files
            m_travelerManager.BackupTravelers("backup\\" + folderName + "\\travelers.json");
            m_orderManager.BackupOrders("backup\\" + folderName + "\\orders.json");
            ConfigManager.Backup("backup\\" + folderName + "\\config.json");
        }
        // Opens a connection to the MAS database
        private void ConnectToData()
        {
            Server.Write("\r{0}", "Connecting to MAS...");

            // initialize the MAS connection
            m_MAS.ConnectionString = "DSN=SOTAMAS90;Company=MGI;";
            m_MAS.ConnectionString = "DSN=SOTAMAS90;Company=MGI;UID=GKC;PWD=sgp4x347;";
            try
            {
                m_MAS.Open();
                Server.Write("\r{0}", "Connecting to MAS...Connected\n");
            }
            catch (Exception ex)
            {
                Server.Write("\r{0}", "Connecting to MAS...Failed\n");
                Server.WriteLine(ex.Message);
            }
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
        private string m_rootDirectory;
        private string m_ip;
        private int m_port;
        private ClientManager m_clientManager;
        private Thread m_clientManagerThread;
        private TimeSpan m_updateInterval;
        private Timer m_timer;
        private TravelerManager m_travelerManager;
        private OrderManager m_orderManager;
        private OdbcConnection m_MAS;
        private static StreamWriter m_outputLog = new StreamWriter(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "server log.txt"));

        // FILE SERVING---------------------------------------
        private HttpListener m_listener;
        private readonly string[] m_indexFiles = {
            "EATS Client/operator.html",
            "EATS Client/supervisor.html",
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
    }
    
}
