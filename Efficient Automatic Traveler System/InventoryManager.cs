using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    static class InventoryManager
    {
        #region Public Methods
        // initializes the user manager from a json file
        static public void Import(DateTime? date = null)
        {
            try
            {
                m_inventory.Clear();
                if (BackupManager.CurrentBackupExists("inventory.json") || date != null)
                {
                    Dictionary<string,string> inventory = (new StringStream(BackupManager.Import("inventory.json", date))).ParseJSON();
                    foreach (KeyValuePair<string,string> inventoryItem in inventory)
                    {
                        m_inventory.Add(inventoryItem.Key, Convert.ToInt16(inventoryItem.Value));
                    }
                    Server.WriteLine("EATS inventory loaded from backup");
                }
            }
            catch (Exception ex)
            {
                Server.WriteLine("Could not load EATS inventory from backup");
                Server.LogException(ex);
            }
        }
        // writes the stored config string back to the config file
        static public void Backup()
        {
            try
            {
                Dictionary<string, string> inventory = new Dictionary<string, string>();
                foreach (KeyValuePair<string, short> inventoryItem in m_inventory)
                {
                    inventory.Add(inventoryItem.Key, inventoryItem.Value.ToString());
                }
                BackupManager.Backup("inventory.json",inventory.Stringify(true));
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
            }
        }

        // returns the user that is requested
        static public int Get(string itemCode)
        {
            if (m_inventory.ContainsKey(itemCode))
            {
                return Convert.ToInt32(m_inventory[itemCode]);
            } else
            {
                return 0;
            }
        }
        static public void Add(string itemCode, int qty = 1)
        {
            if (m_inventory.ContainsKey(itemCode))
            {
                m_inventory[itemCode] += Convert.ToInt16(qty);
            }
            else
            {
                m_inventory.Add(itemCode, Convert.ToInt16(qty));
            }
        }
        static public Dictionary<string, short> Inventory
        {
            get { return m_inventory; }
        }
        #endregion
        #region Private Methods
        #endregion
        #region Properties
        private static Dictionary<string, short> m_inventory;
        #endregion

    }
}
