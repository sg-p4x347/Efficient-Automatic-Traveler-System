using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    static class ConfigManager
    {
        #region Public Methods
        // initializes the config manager from a json file
        static public void Open(string file)
        {
            m_configObj = (new StringStream(System.IO.File.ReadAllText(System.IO.Path.Combine(Server.RootDir, file)))).ParseJSON();
            Server.WriteLine("Configuration settings loaded");
        }
        // writes the stored config string back to the config file
        static public void Backup(string file = "config.json")
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(Server.RootDir, file), m_configObj.Stringify(true));
        }
        // returns the json string stored under the specified key
        static public string Get(string key)
        {
            return m_configObj.ContainsKey(key) ? m_configObj[key] : "";
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
