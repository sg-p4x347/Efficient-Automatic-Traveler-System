using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Efficient_Automatic_Traveler_System
{
    public class BackupManager
    {
        #region Public Methods
        static internal void Initialize()
        {
            
            CreateBackupDir();

            string[] backupPaths = System.IO.Directory.GetDirectories(System.IO.Path.Combine(Server.RootDir, "backup\\"));
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
        // gets the most recent past backup
        static internal DateTime GetMostRecent()
        {
            return m_backupDates.First(x => x.Date < DateTime.Today.Date);
        }
        // makes a backup folder for today's date
        static internal void CreateBackupDir()
        {
            Directory.CreateDirectory(Path.Combine(Server.RootDir, "backup", DateToString(DateTime.Today)));
        }
        // returns true if a current backup for today exists
        static internal bool CurrentBackupExists(string file)
        {
            return  (m_backupDates.Exists(x => x == DateTime.Today.Date)
                && File.Exists(Path.Combine(Server.RootDir, "backup", DateToString(DateTime.Today.Date),file)));
        }
        // returns the requested file from current day backup
        static internal string Import(string filename,DateTime? d = null)
        {
            DateTime date = (d == null ? DateTime.Today.Date : d.Value);
            // if there is a backup for today
            if (m_backupDates.Exists(x => x.Date == date))
            {
                // if the file exists
                if (File.Exists(Path.Combine(Server.RootDir, "backup", DateToString(date), filename)))
                {
                    List<Traveler> travelers = new List<Traveler>();
                    // return the file text
                    return File.ReadAllText(Path.Combine(Server.RootDir, "backup", DateToString(date), filename));
                }
            }
            return "";
        }
        // returns the requested file from most recent backup that is not the current day
        static internal string ImportPast(string filename)
        {
            // if there is a backup from a previous day
            if (m_backupDates.Exists(x => x.Date < DateTime.Today.Date))
            {
                DateTime date = m_backupDates.First(x => x.Date < DateTime.Today.Date);
                // if the file exists
                if (File.Exists(Path.Combine(Server.RootDir, "backup", DateToString(date), filename)))
                {
                    List<Traveler> travelers = new List<Traveler>();
                    // open the file
                    return File.ReadAllText(Path.Combine(Server.RootDir, "backup", DateToString(date), filename));
                }
            }
            return "";
        }
        static internal void Backup(string filename, string contents)
        {
            CreateBackupDir();
            File.WriteAllText(Path.Combine(Server.RootDir, "backup", DateToString(DateTime.Today.Date), filename),contents);
        }
        static internal void Backup(string path)
        {
            CreateBackupDir();
            string filename = Path.GetFileName(path);
            System.IO.File.Copy(
                path,
                Path.Combine(Server.RootDir, "backup", DateToString(DateTime.Today.Date), filename), true
            );
        }

        static internal T ImportDerived<T>(string json)
        {
            Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
            T derived = default(T);
            if (obj["type"] != "")
            {
                Type type = Type.GetType(obj["type"]);
                derived = (T)Activator.CreateInstance(type, json);
            }
            return derived;
        }
        #endregion
        #region Static Properties
        private static List<DateTime> m_backupDates;
        #endregion
    }
}
