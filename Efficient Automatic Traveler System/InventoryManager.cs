using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Odbc;

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
                } else
                {
                    ImportPast();
                }
            }
            catch (Exception ex)
            {
                Server.WriteLine("Could not load EATS inventory from backup");
                Server.LogException(ex);
            }
        }
        static public void ImportPast()
        {
            try
            {
                Dictionary<string, string> inventory = (new StringStream(BackupManager.ImportPast("inventory.json"))).ParseJSON();
                foreach (KeyValuePair<string, string> inventoryItem in inventory)
                {
                    m_inventory.Add(inventoryItem.Key, Convert.ToInt16(inventoryItem.Value));
                }
                Server.WriteLine("EATS inventory loaded from backup");
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
                foreach (KeyValuePair<string, int> inventoryItem in m_inventory)
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
        static public async Task<Dictionary<string, int>> GetCurrentMASinventory(List<string> itemCodes)
        {
            
            Dictionary<string, int> inventory = new Dictionary<string, int>();
            if (itemCodes != null && itemCodes.Count > 0)
            {
                OdbcConnection MAS = new OdbcConnection();
                MAS.ConnectionString = "DSN=SOTAMAS90;Company=MGI;UID=GKC;PWD=sgp4x347;";
                MAS.Open();
                if (MAS.State == System.Data.ConnectionState.Open)
                {
                    OdbcCommand command = MAS.CreateCommand();
                    string sqlList = "(";
                    bool first = true;
                    foreach (string itemCode in itemCodes)
                    {
                        if (!first) sqlList += ',';
                        first = false;
                        sqlList += "'" + itemCode + "'";
                    }
                    sqlList += ')';
                    command.CommandText = "SELECT ItemCode, QuantityOnHand FROM IM_ItemWarehouse WHERE ItemCode IN " + sqlList;
                    OdbcDataReader reader = (OdbcDataReader)await command.ExecuteReaderAsync();
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(0) && !reader.IsDBNull(1) && !inventory.ContainsKey(reader.GetString(0)))
                        {
                            inventory.Add(reader.GetString(0), Convert.ToInt32(reader.GetValue(1)));
                        }
                    }
                    reader.Close();
                }
            }
            return inventory;
        }
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
        static public int GetMAS(string itemCode)
        {
            if (m_MASinventory.ContainsKey(itemCode))
            {
                return Convert.ToInt32(m_MASinventory[itemCode]);
            }
            else
            {
                return 0;
            }
        }
        static public void Add(string itemCode, int qty = 1)
        {
            if (m_inventory.ContainsKey(itemCode))
            {
                m_inventory[itemCode] += Convert.ToInt16(qty);
            } else
            {
                m_inventory.Add(itemCode, Convert.ToInt16(qty));
            }
            Backup();
        }
        static public void Set(string itemCode, int qty)
        {
            m_inventory[itemCode] = qty;
        }
        static public void SetMAS(string itemCode, int qty)
        {
            m_MASinventory[itemCode] = qty;
        }
        static public Dictionary<string, int> Inventory
        {
            get { return m_inventory; }
        }
        static public Dictionary<string, int> MASinventory
        {
            get { return m_MASinventory; }
        }
        #endregion
        #region Private Methods
        #endregion
        #region Properties
        private static Dictionary<string, int> m_inventory = new Dictionary<string, int>();
        private static Dictionary<string, int> m_MASinventory = new Dictionary<string, int>();
        #endregion

    }
}
