using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Efficient_Automatic_Traveler_System
{
    public enum Version
    {
        _2_4,
        _2_6

    }
    public class BackupManager
    {
        #region Public Methods
        static public void Initialize(string rootDir = null)
        {
            
            //CreateBackupDir();

            string[] backupPaths = System.IO.Directory.GetDirectories(System.IO.Path.Combine(rootDir != null ? rootDir : Server.RootDir, "backup\\"));
            m_backupDates = new List<DateTime>();
            foreach (string path in backupPaths)
            {
                m_backupDates.Add(StringToDate(Path.GetFileName(path)));
            }
            // sort descending 
            m_backupDates.Sort((x, y) => y.CompareTo(x));
        }
        static public string GetBackupDates(string json)
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
        static public string DateToString(DateTime date)
        {
            return date.ToString("MM-dd-yyyy");
        }
        // Standardized conversion from string to dateTime
        static public DateTime StringToDate(string date)
        {
            return DateTime.Parse(date);
        }
        // gets the most recent past backup
        static public DateTime GetMostRecent()
        {
            return m_backupDates.First(x => x.Date < DateTime.Today.Date);
        }
        // makes a backup folder for today's date
        static public void CreateBackupDir()
        {
            Directory.CreateDirectory(Path.Combine(Server.RootDir, "backup", DateToString(DateTime.Today)));
            // mirror in dataDump
            Directory.CreateDirectory(Path.Combine(ConfigManager.Get("dataDump"), "backup", DateToString(DateTime.Today)));
        }
        // returns true if a current backup for today exists
        static public bool CurrentBackupExists(string file)
        {
            return  (m_backupDates.Exists(x => x == DateTime.Today.Date)
                && File.Exists(Path.Combine(Server.RootDir, "backup", DateToString(DateTime.Today.Date),file)));
        }
        // returns true if the file exists
        static public bool BackupExists(string file, DateTime date)
        {
            return (m_backupDates.Exists(x => x == date)
                && File.Exists(Path.Combine(Server.RootDir, "backup", DateToString(DateTime.Today.Date), file)));
        }
        // returns the requested file from current day backup
        static public string Import(string filename,DateTime? d = null)
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
        static public string ImportPast(string filename)
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
        static public void Backup(string filename, string contents)
        {
            Version version = Version._2_6;
            CreateBackupDir();
            contents = version.ToString() + '\n' + contents;
            File.WriteAllText(Path.Combine(Server.RootDir, "backup", DateToString(DateTime.Today.Date), filename),contents);
            // mirror this at the data dump location
            File.WriteAllText(Path.Combine(ConfigManager.Get("dataDump"), "backup", DateToString(DateTime.Today.Date), filename), contents);
        }
        static public void Backup(string path)
        {
            CreateBackupDir();
            string filename = Path.GetFileName(path);
            System.IO.File.Copy(
                path,
                Path.Combine(Server.RootDir, "backup", DateToString(DateTime.Today.Date), filename), true
            );
        }

        static public T ImportDerived<T>(string json)
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

        public static List<DateTime> BackupDates
        {
            get
            {
                return m_backupDates;
            }
        }
        public static void GetVersion(string text, out string detail, out Version version)
        {
            version = Version._2_4;
            if (!Enum.TryParse(text.GetLine(),out version )) {
                version = Version._2_4;
            }
            detail = text;
        }
        #endregion
    }
}
