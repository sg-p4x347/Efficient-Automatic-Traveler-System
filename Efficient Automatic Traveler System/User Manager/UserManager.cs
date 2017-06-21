using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    public class UserManager : IManager
    {
        #region Public Methods
        // initializes the user manager from a json file
        public void Import(DateTime? date = null)
        {
            try
            {
                m_users.Clear();
                if (BackupManager.CurrentBackupExists("users.json") || date != null)
                {
                    List<string> userArray = (new StringStream(BackupManager.Import("users.json", date))).ParseJSONarray();
                    foreach (string userJSON in userArray)
                    {
                        User user = new User(userJSON);
                        m_users.Add(user);
                    }
                    Server.WriteLine("Users loaded from backup");
                } else
                {
                    ImportPast();
                }
            } catch (Exception ex)
            {
                Server.WriteLine("Could not load users from backup");
                Server.LogException(ex);
            }
        }
        public void ImportPast()
        {
            try
            {
                List<string> userArray = (new StringStream(BackupManager.ImportPast("users.json"))).ParseJSONarray();
                foreach (string userJSON in userArray)
                {
                    User user = new User(userJSON);
                    user.History.RemoveAll(x => x.Date < DateTime.Today.Date);
                    m_users.Add(user);
                }
                Server.WriteLine("Users loaded from backup");
            }
            catch (Exception ex)
            {
                Server.WriteLine("Could not load users from backup");
                Server.LogException(ex);
            }
        }
        // writes the stored config string back to the config file
        public void Backup()
        {
            try
            {
                BackupManager.Backup("users.json", Export() );
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
            }
        }
        public string Export()
        {
            return m_users.Stringify<User>();
        }
        public void AddUser(User user)
        {
            m_users.Add(user);
            Backup();
        }
        
        // returns the user that is requested
        public User Find(string searchPhrase)
        {
            return m_users.Find(x => x.UID == searchPhrase || x.Name.ToLower() == searchPhrase.ToLower());
        }
        #endregion
        #region Private Methods
        #endregion
        #region Properties
        private List<User> m_users = new List<User>();

        public List<User> Users
        {
            get
            {
                return m_users;
            }
        }
        #endregion
    }
}
