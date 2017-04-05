using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    static class UserManager
    {
        #region Public Methods
        // initializes the user manager from a json file
        static public void Open(string file)
        {
            try
            {
                List<string> list = (new StringStream(System.IO.File.ReadAllText(System.IO.Path.Combine(Server.RootDir, file)))).ParseJSONarray();
                foreach (string item in list)
                {
                    m_users.Add(new User(item));
                }
                Server.WriteLine("Users loaded");
            } catch (Exception ex)
            {
                Server.WriteLine("Could not load users");
                Server.LogException(ex);
            }
        }
        static public void AddUser(User user)
        {
            m_users.Add(user);
            Backup();
        }
        // writes the stored config string back to the config file
        static public void Backup(string file = "users.json")
        {
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(Server.RootDir, file), m_users.Stringify());
            } catch (Exception ex)
            {
                Server.LogException(ex);
            }
        }
        // returns the user that is requested
        static public User Find(string searchPhrase)
        {
            return m_users.Find(x => x.UID == searchPhrase || x.Name == searchPhrase);
        }
        #endregion
        #region Private Methods
        #endregion
        #region Properties
        static private List<User> m_users = new List<User>();
        #endregion
    }
}
