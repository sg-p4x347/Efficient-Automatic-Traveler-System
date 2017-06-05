using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    internal delegate void KanbanChangedSubscriber();
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
                    Server.TravelerManager.GetTravelers.OfType<IPart>().Where(t => t.ItemCode == itemCodeQty.Key).Sum(x => ((Traveler)x).Quantity)
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
            Dictionary<string, string> flexStart = new Dictionary<string, string>() { { "justifyContent", @"""flex-start""" } };
            Dictionary<string, string> center = new Dictionary<string, string>() { { "align-items", @"""center""" } };
            
            Column controls = new Column(style: flexStart)
            {
                new Button("New Item","NewKanbanItemForm")
            };
            NodeList monitorTable = new NodeList(BorderStyle,DOMtype: "table");
            monitorTable.Add(KanbanItem.CreateMonitorHeader());
            foreach (KanbanItem item in m_items)
            {
                monitorTable.Add(item.CreateMonitorRow());
            }
            return new ControlPanel("Kanban Monitor", new Row(true) { controls, monitorTable });
        }
        public static ClientMessage NewKanbanItemForm( string json)
        {
            return new ClientMessage("NewKanbanItemForm", KanbanItem.CreateForm().ToString());
        }
        public static async Task<ClientMessage> NewKanbanItem(string json)
        {
            try
            {
                Form form = new Form(json);
                KanbanItem newItem = new KanbanItem(form);
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
        private static void HandleKanbanChanged()
        {
            Backup();
            KanbanChanged();
        }
        public static event KanbanChangedSubscriber KanbanChanged = delegate { };

        private static List<KanbanItem> m_items = new List<KanbanItem>();
        private static TimeSpan m_updateInterval;
        private static Timer m_timer;
        
        public static Dictionary<string, string> BorderStyle = new Dictionary<string, string>()
        {
            {"border","2px solid black".Quotate() }
        };
    }
}
