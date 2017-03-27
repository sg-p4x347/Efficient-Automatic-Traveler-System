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
        static public ClientMessage GetBackupDates(string json)
        {
            ClientMessage returnMessage;
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                //Summary summary = new Summary(this as ITravelerManager);
                string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string[] backupFolders = System.IO.Directory.GetDirectories(System.IO.Path.Combine(exeDir, "backup\\"));
                List<string> backupDates = new List<string>();
                foreach (string folder in backupFolders)
                {
                    backupDates.Add(folder);
                }
                returnMessage = new ClientMessage("PopulateSummaryFrom", backupDates.Stringify());
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
                returnMessage = new ClientMessage("Info", "error");
            }
            return returnMessage;
        }
    }
}
