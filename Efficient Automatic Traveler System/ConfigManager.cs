using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
namespace Efficient_Automatic_Traveler_System
{
    static class ConfigManager
    {
        #region Public Methods
        // initializes the config manager from a json file
        static public void Import(DateTime? date = null)
        {
            try
            {
                if (date == null)
                {
                    string path = Path.Combine(Server.RootDir, "config.json");
                    m_configObj = (new StringStream(File.ReadAllText(path))).ParseJSON();
                    Server.WriteLine("Configuration settings loaded from current");
                } else
                {
                    m_configObj = (new StringStream(BackupManager.Import("config.json",date))).ParseJSON();
                    Server.WriteLine("Configuration settings loaded from " + BackupManager.DateToString(date.Value));
                }
                
            } catch (Exception ex)
            {
                Server.LogException(ex);
                Server.WriteLine("Failed to load configuration settings");
            }
        }
        // writes the stored config string back to the config file
        static public void Backup()
        {
            string path = Path.Combine(Server.RootDir, "config.json");
            File.WriteAllText(path, Export(true));
            BackupManager.Backup(path);
        }
        static public string Export(bool pretty = false)
        {
            return m_configObj.Stringify(pretty);
        }
        // returns the json string stored under the specified key
        static public string Get(string key)
        {
            if (m_configObj.ContainsKey(key))
            {
                return m_configObj.ContainsKey(key) ? m_configObj[key] : "";
            } else
            {
                return "";
            }
        }
        // sets the json string stored under the specified key
        static public void Set(string key, string value)
        {
            m_configObj[key] = value;
            Backup();

        }
        #endregion
        #region Private Methods
        #endregion
        #region Properties
        static private Dictionary<string, string> m_configObj;
        #endregion
    }
}
