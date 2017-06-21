using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    public delegate void KanbanChangedSubscriber();
    static class KanbanManager
    {
        // initializes the user manager from a json file
        static public void Import(DateTime? date = null)
        {
            try
            {
                m_items.Clear();
                if (BackupManager.CurrentBackupExists("kanban.json") || date != null)
                {
                    Dictionary<string, string> kanban = (new StringStream(BackupManager.Import("kanban.json", date))).ParseJSON();
                    List<string> items = new StringStream(kanban["items"]).ParseJSONarray();
                    foreach (string kanbanItem in items)
                    {
                        m_items.Add(new KanbanItem(kanbanItem));
                    }
                    Server.WriteLine("Kanban Monitor loaded from backup");
                }
                else
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
                Dictionary<string, string> kanban = (new StringStream(BackupManager.ImportPast("kanban.json"))).ParseJSON();
                List<string> items = new StringStream(kanban["items"]).ParseJSONarray();
                foreach (string kanbanItem in items)
                {
                    m_items.Add(new KanbanItem(kanbanItem));
                }
                Server.WriteLine("Kanban Manager loaded from backup");
            }
            catch (Exception ex)
            {
                Server.WriteLine("Could not load Kanban Manager from backup");
                Server.LogException(ex);
            }
        }
        // writes the stored config string back to the config file
        static public void Backup()
        {
            try
            {
                Dictionary<string, string> kanban = new Dictionary<string, string>();
                List<string> items = new List<string>();
                foreach (KanbanItem item in m_items)
                {
                    items.Add(item.ToString());
                }
                kanban.Add("items", items.Stringify(false));
                BackupManager.Backup("kanban.json", kanban.Stringify(true));
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
            }
        }
        // Starts an update cycle that keeps inventory relatively up-to-date throughout the day
        static public async void Start()
        {
            m_updateInterval = TimeSpan.FromHours(Convert.ToDouble(ConfigManager.Get("KanbanManagerSyncPeriod")));
            await Update();
            UpdateTimer();
        }
        static private async Task Update()
        {
            List<string> itemCodes = m_items.Select(x => x.ItemCode).ToList();
            Dictionary<string,int> inventory = await InventoryManager.GetCurrentMASinventory(itemCodes);
            foreach (KeyValuePair<string, int> itemCodeQty in inventory)
            {
                KanbanItem item = m_items.Find(i => i.ItemCode == itemCodeQty.Key);
                item.Update(
                    itemCodeQty.Value,
                    Server.TravelerManager.GetTravelers.Where(t => t.ItemCode == itemCodeQty.Key).Sum(x => ((Traveler)x).Quantity)
                );
            }
            HandleKanbanChanged();
        }
        static private void UpdateTimer()
        {
            DateTime current = DateTime.Now;
            TimeSpan timeToGo = current.RoundUp(m_updateInterval).TimeOfDay - current.TimeOfDay;
            if (timeToGo.Ticks < 0) timeToGo = timeToGo.Add(new TimeSpan(24, 0, 0));
            m_timer = new System.Threading.Timer(x =>
            {
                Update();
                UpdateTimer();
            }, null, timeToGo, Timeout.InfiniteTimeSpan);
        }
        public static ControlPanel CreateKanbanMonitor()
        {
            Row controls = new Row(style: new Style("justify-start"))
            {
                new Button("New Item","NewKanbanItemForm")
            };
            NodeList monitorTable = new NodeList(new Style("kanban__border"),DOMtype: "table");
            monitorTable.Add(KanbanItem.CreateMonitorHeader());
            int rowIndex = 0;
            foreach (KanbanItem item in m_items)
            {
                monitorTable.Add(item.CreateMonitorRow(rowIndex));
                rowIndex++;
            }
            return new ControlPanel("Kanban Monitor", new Column(true) { controls, monitorTable });
        }
        public static ClientMessage NewKanbanItemForm( string json)
        {
            return new ClientMessage("NewKanbanItemForm", KanbanItem.CreateForm().ToString());
        }
        public static async Task<ClientMessage> NewKanbanItem(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Form form = new Form(json);
                KanbanItem newItem = new KanbanItem(form);
                if (m_items.Exists(i => i.ItemCode == newItem.ItemCode)) return new ClientMessage("Info", "A Kanban item for " + newItem.ItemCode + " already exists");
                m_items.Add(newItem);
                await Update();
                return new ClientMessage("ControlPanel", CreateKanbanMonitor().ToString());
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error when adding new Kanban Item");
            }

        }
        public static ClientMessage EditKanbanItemForm(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();

                return m_items.Find(i => i.ItemCode == obj["itemCode"]).CreateFilledForm().Dispatch("EditKanbanItem");
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error when creating KanbanItem edit form");
            }
        }
        public static async Task<ClientMessage> EditKanbanItem(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Form form = new Form(obj["form"]);
                KanbanItem newItem = new KanbanItem(form);
                m_items.Find(i => i.ItemCode == newItem.ItemCode).Update(form);
                await Update();
                return new ClientMessage("ControlPanel", CreateKanbanMonitor().ToString());
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error when editing Kanban Item");
            }
        }
        public static ClientMessage DeleteKanbanItem(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                m_items.RemoveAll(i => i.ItemCode == obj["itemCode"]);
                return new ClientMessage("ControlPanel", CreateKanbanMonitor().ToString());
            } catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error when deleting KanbanItem");
            }
        }
        private static void HandleKanbanChanged()
        {
            Backup();
            KanbanChanged();
        }
        public static event KanbanChangedSubscriber KanbanChanged = delegate { };

        private static List<KanbanItem> m_items = new List<KanbanItem>();
        private static TimeSpan m_updateInterval;
        private static Timer m_timer;
        public static int ItemCount
        {
            get
            {
                return m_items.Count;
            }
        }
    }
}
