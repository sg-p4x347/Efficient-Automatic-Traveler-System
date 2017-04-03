using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Efficient_Automatic_Traveler_System
{
    public static class BackupManager
    {
        #region Public Methods
        static internal void Initialize()
        {
            string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string[] backupPaths = System.IO.Directory.GetDirectories(System.IO.Path.Combine(exeDir, "backup\\"));
            m_backupDates = new List<DateTime>();
            foreach (string path in backupPaths)
            {
                m_backupDates.Add(StringToDate(Path.GetFileName(path)));
            }
            // sort descending 
            m_backupDates.Sort((x, y) => y.CompareTo(x));
        }
        static internal string GetBackupDates(string json)
        {
            ClientMessage returnMessage;
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                //Summary summary = new Summary(this as ITravelerManager);
                List<string> dateStrings = new List<string>();
                foreach (DateTime date in m_backupDates)
                {
                    dateStrings.Add(DateToString(date));
                }
                
                returnMessage = new ClientMessage("PopulateSummaryFrom", dateStrings.Stringify());
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
                returnMessage = new ClientMessage("Info", "error");
            }
            return returnMessage.ToString();
        }
        // Standardized conversion from dateTime to string
        static internal string DateToString(DateTime date)
        {
            return date.ToString("MM-dd-yyyy");
        }
        // Standardized conversion from string to dateTime
        static internal DateTime StringToDate(string date)
        {
            return DateTime.Parse(date);
        }
        static internal List<Traveler> ImportStoredTravelers()
        {
            List<Traveler> filteredTravelers = new List<Traveler>();
            // if there is a backup from a previous day
            if (m_backupDates.Exists(x => x.Date <= DateTime.Today.Date))
            {
                DateTime date = m_backupDates.First(x => x.Date <= DateTime.Today.Date);
                // if the file exists
                if (File.Exists(Path.Combine(Server.RootDir, "backup", DateToString(date), "travelers.json")))
                {
                    List<Traveler> travelers = new List<Traveler>();
                    // open the file
                    List<string> lines = File.ReadLines(Path.Combine(Server.RootDir, "backup", DateToString(date), "travelers.json")).ToList();
                    int index = 0;
                    foreach (string line in lines)
                    {
                        Server.Write("\r{0}%", "Loading travelers from backup..." + Convert.ToInt32((Convert.ToDouble(index) / lines.Count) * 100));

                        Dictionary<string, string> obj = (new StringStream(line)).ParseJSON();
                        // check to see if these orders have been printed already
                        // cull orders that do not exist anymore
                        Traveler traveler = null;
                        if (obj["type"] != "")
                        {
                            Type type = Type.GetType(obj["type"]);
                            traveler = (Traveler)Activator.CreateInstance(type,line);
                        }

                        if (traveler != null)
                        {
                            travelers.Add(traveler);
                        }
                        index++;
                    }
                    Server.Write("\r{0}", "Loading travelers from backup...Finished\n");

                    // if travelers are imported from previous day, filter out completed items
                    if (!m_backupDates.Exists(x => x.Date == DateTime.Today.Date))
                    {
                        foreach (Traveler traveler in travelers)
                        {
                            // add this traveler to the master list if it is not complete
                            if (traveler.State != ItemState.PostProcess)
                            {
                                // push this traveler into production
                                if (traveler.State == ItemState.PreProcess && traveler.Station != StationClass.GetStation("Start"))
                                {
                                    traveler.EnterProduction();
                                }
                                // add this traveler to the filtered list
                                
                                filteredTravelers.Add(traveler);
                            }

                        }
                    } else
                    {
                        filteredTravelers = travelers;
                    }
                }
            }
            return filteredTravelers;
        }
        static internal List<Order> ImportStoredOrders()
        {
            List<Order> filteredOrders = new List<Order>();
            // if there is a backup from a previous day
            if (m_backupDates.Exists(x => x.Date <= DateTime.Today.Date))
            {
                DateTime date = m_backupDates.First(x => x.Date <= DateTime.Today.Date);
                // if the file exists
                if (File.Exists(Path.Combine(Server.RootDir, "backup", DateToString(date), "orders.json")))
                {
                    List<Order> orders = new List<Order>();
                    // open the file
                    List<string> lines = File.ReadLines(Path.Combine(Server.RootDir, "backup", DateToString(date), "orders.json")).ToList();
                    int index = 0;
                    foreach (string line in lines)
                    {
                        Server.Write("\r{0}%", "Loading orders from backup..." + Convert.ToInt32((Convert.ToDouble(index) / lines.Count) * 100));
                        orders.Add(new Order(line));
                    }
                    Server.Write("\r{0}", "Loading orders from backup...Finished\n");

                    // if travelers are imported from previous day, filter out completed items
                    if (!m_backupDates.Exists(x => x.Date == DateTime.Today.Date))
                    {
                        foreach (Order order in orders)
                        {
                            // add this order to the master list if it is not closed
                            if (order.State != OrderState.Closed)
                            {
                                filteredOrders.Add(order);
                            }

                        }
                    }
                    else
                    {
                        filteredOrders = orders;
                    }
                }
            }
            return filteredOrders;
        }
        static internal void BackupTravelers(List<Traveler> travelers)
        {
            CreateBackupDir();
            string contents = "";
            foreach (Traveler traveler in travelers)
            {
                contents += traveler.ToString();
                contents += '\n';
            }

            System.IO.File.WriteAllText(Path.Combine(Server.RootDir, "backup", DateToString(DateTime.Today), "travelers.json"), contents);
        }
        static internal void BackupOrders(List<Order> orders)
        {
            CreateBackupDir();
            string contents = "";
            foreach (Order order in orders)
            {
                contents += order.ToString();
                contents += '\n';

            }
            System.IO.File.WriteAllText(Path.Combine(Server.RootDir,"backup", DateToString(DateTime.Today), "orders.json"), contents);
        }
        static internal void BackupConfig()
        {
            CreateBackupDir();
            System.IO.File.Copy(Path.Combine(Server.RootDir, "config.json"),
                Path.Combine(Server.RootDir, "backup", DateToString(DateTime.Today), "config.json"),true);
        }
        static internal void CreateBackupDir()
        {
            Directory.CreateDirectory(Path.Combine(Server.RootDir, "backup", DateToString(DateTime.Today)));
        }
        #endregion
        #region Static Properties
        private static List<DateTime> m_backupDates;
        #endregion
    }
}
