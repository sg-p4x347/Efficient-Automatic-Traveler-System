using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Odbc;
using System.Diagnostics;
using System.IO;
using System.Reflection;
namespace Efficient_Automatic_Traveler_System
{
    // Class: Used to generate and store the digital "travelers" that are used throughout the system
    // Developer: Gage Coates
    // Date started: 1/25/17
    public interface ITravelerManager : IOperatorActions, ISupervisorActions
    {
        //void CreateScrapChild(Traveler parent, int qtyScrapped);
        //Traveler CreateCompletedChild(Traveler parent, int qtyMade, double time);
        Traveler FindTraveler(int ID);
        bool FindTraveler(int ID, out Traveler traveler);
        void RemoveTraveler(Traveler traveler, bool backup = true);
        Traveler AddTraveler(string itemCode, int quantity);
        List<Traveler> GetTravelers
        {
            get;
        }
        void Backup();
        void OnTravelersChanged(List<Traveler> travelers = null);
        void OnTravelersChanged(Traveler traveler);
        void RefactorTravelers();
        void ClearStartQueue();
        void CreateBoxTravelers();
    }
    public interface IOperatorActions
    {
        //ClientMessage AddTravelerEvent(ProcessEvent itemEvent, Traveler traveler, TravelerItem travelerItem);
        //ClientMessage SubmitTraveler(Traveler traveler, StationClass station);
    }
    public interface ISupervisorActions
    {
        string MoveTravelerStart(string json);
        ClientMessage LoadTravelerJSON(string json);
        //ClientMessage LoadTravelerAt(string json);
        //ClientMessage LoadItem(string json);
        ClientMessage CreateSummary(string json);
        ClientMessage EnterProduction(string json);
        ClientMessage DownloadSummary(string json);
        ClientMessage TravelerForm(string json);
        Task<ClientMessage> NewTraveler(string json);
    }
    public delegate void TravelersChangedSubscriber(List<Traveler> travelers);
    public class TravelerManager : IManager, ITravelerManager
    {
        #region Public methods
        public TravelerManager(IOrderManager orderManager)
        {
            TravelersChanged = delegate { };
            m_travelers = new List<Traveler>();
            m_importedFromPast = new List<Traveler>();
            m_orderManager = orderManager;
        }
        // returns list of new travelers
        public List<Traveler> CompileTravelers(bool tables, bool consolodate, bool consolidatePriorityCustomers,  List<Order> orders)
        {
            List<Traveler> newTravelers = new List<Traveler>();
            int index = 0;
            foreach (Order order in orders)
            {
                if (order.Status == OrderStatus.Open)
                {
                    foreach (OrderItem item in order.Items.Where(i => i.ItemStatus == OrderStatus.Open && (!tables || Traveler.IsTable(i.ItemCode))))
                    {
                        // only make a traveler if this one has no child traveler already (-1 signifies no child traveler)
                        if (item.ChildTraveler < 0)
                        {
                            Server.Write("\r{0}%", "Compiling Travelers..." + Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(m_orderManager.GetOrders.Count)) * 100));

                            // search for existing traveler
                            // can only combine if same itemCode, hasn't started, and has no parents
                            Traveler traveler = newTravelers.Find(x => x.CombinesWith(new object[] { item.ItemCode }));
                            
                            
                            if (traveler != null) {
                                bool containsPriority = traveler.ParentOrders.Exists(o => ((JsonArray)JSON.Parse(ConfigManager.Get("priorityCustomers"))).ToList().Contains(o.CustomerNo));
                                bool isPriority = ((JsonArray)JSON.Parse(ConfigManager.Get("priorityCustomers"))).ToList().Contains(order.CustomerNo);
                                if ((consolidatePriorityCustomers && (isPriority == containsPriority)) || (consolodate && !consolidatePriorityCustomers))
                                {
                                    if (!traveler.ParentOrderNums.Contains(order.SalesOrderNo))
                                    {
                                        // add to existing traveler
                                        //traveler.Quantity += quantity;

                                        // RELATIONAL =============================================================
                                        traveler.ParentOrderNums.Add(order.SalesOrderNo);
                                        traveler.ParentOrders.Add(order);
                                    }
                                    item.ChildTraveler = traveler.ID;
                                    //=========================================================================
                                }
                            }
                            else
                            {
                                // create a new traveler from the new item
                                Traveler newTraveler = null;
                                if (Traveler.IsTable(item.ItemCode)) {
                                    newTraveler = (Traveler)new Table(item.ItemCode, -0);
                                } else if (Traveler.IsChair(item.ItemCode)) {
                                    newTraveler = (Traveler)new Chair(item.ItemCode, -0);
                                }
                                if (newTraveler != null)
                                {
                                    // RELATIONAL =============================================================
                                    item.ChildTraveler = newTraveler.ID;
                                    newTraveler.ParentOrderNums.Add(order.SalesOrderNo);
                                    newTraveler.ParentOrders.Add(order);
                                    //=========================================================================

                                    // add the new traveler to the list
                                    newTravelers.Add(newTraveler);
                                }
                            }
                        }
                    }
                }
                index++;
            }
            // allocate inventory and set final traveler quantities
            foreach (Traveler newTraveler in newTravelers)
            {
                // quantity to add to the traveler maxes out at qty ordered, taking into account what
                // is on hand that hasnt been allocated for another traveler;

                // total allocated is the sum of what has been ordered for all active travelers
                int qtyAllocated = m_travelers.Where(t => t.ItemCode == newTraveler.ItemCode).Sum(t => t.QuantityOrdered());
                int qtyOnHand = InventoryManager.GetMAS(newTraveler.ItemCode);
                int qtyOrdered = newTraveler.QuantityOrdered();
                newTraveler.Quantity = qtyOrdered - Math.Min(qtyOrdered, Math.Max(0,qtyOnHand - qtyAllocated));
                m_travelers.Add(newTraveler);
            }
            
            Backup();
            Server.Write("\r{0}", "Compiling Travelers...Finished\n");
            return newTravelers;
        }
        public void CullFinishedTravelers()
        {
            // remove all traveler trees that were finished before today
            List<Traveler> travelers = m_travelers.Where(t => t.FinishedBefore(DateTime.Today) && t.ChildTravelers.All(child => child.FinishedBefore(DateTime.Today)) && t.ParentTravelers.All(parent => parent.FinishedBefore(DateTime.Today))).ToList();
            Server.WriteLine(travelers.Stringify());
        }
        public void ImportTravelerInfo(IOrderManager orderManager, ref OdbcConnection MAS,List<Traveler> travelers = null,Action<double> ReportProgress = null)
        {
            if (travelers == null) travelers = m_travelers;
            int index = 0;
            foreach (Traveler traveler in travelers)
            {
                try
                {

                    traveler.InitializeDependencies();
                    // import part info
                    traveler.ImportInfo(this as ITravelerManager, orderManager, MAS);
                    index++;
                    double percent = (Convert.ToDouble(index) / Convert.ToDouble(travelers.Count));
                    ReportProgress?.Invoke(percent);


                    Server.Write("\r{0}%", "Gathering Info..." + Math.Round(percent * 100));
                }
                catch (Exception ex)
                {
                    Server.LogException(ex);
                }
            }
            Server.Write("\r{0}", "Gathering Info...Finished\n");
            // travelers have changed
            OnTravelersChanged(travelers);
        }
        //// Update this travelers quantities dynamically
        //public void UpdateTraveler(Traveler traveler)
        //{

        //}
        public void RefactorTravelers()
        {
            Server.OrderManager.RefactorOrders();
            // only change the quantities of preprocess travelers
            foreach (Traveler traveler in m_travelers.Where(t => t.State == GlobalItemState.PreProcess))
            {
                List<OrderItem> items = traveler.ParentOrders.SelectMany(o => o.Items.Where(i => i.ChildTraveler == traveler.ID)).ToList();
                traveler.Quantity = items.Sum(i => i.QtyOrdered - i.QtyOnHand);
            }
            OnTravelersChanged();
        }
        public void CreateBoxTravelers()
        {
            foreach (Table table in new List<Table>(m_travelers.OfType<Table>()))
            {
                table.CreateBoxTraveler();
            }
            OnTravelersChanged();
        }
        #endregion
        //----------------------------------
        #region IManager
        public void Import(DateTime? date = null)
        {

            m_travelers.Clear();
            if (BackupManager.CurrentBackupExists("travelers.json") || date != null)
            {
                string travelerText = "";
                Version version;
                BackupManager.GetVersion(BackupManager.Import("travelers.json", date),out  travelerText,out version);


                List<string> travelerArray = (new StringStream(travelerText)).ParseJSONarray();
                Server.Write("\r{0}", "Loading travelers from backup...");
                foreach (string travelerJSON in travelerArray)
                {
                    Traveler traveler = ImportTraveler(travelerJSON,version);
                    if (traveler != null)
                    {
                        m_travelers.Add(traveler);
                    }
                }
                Server.Write("\r{0}", "Loading travelers from backup...Finished" + Environment.NewLine);
            } else
            {
                ImportPast();
            }
            // link dem
            LinkTravelers();
        }
        public void ImportPast()
        {
            m_travelers.Clear();
            m_importedFromPast.Clear();
            string travelerText = "";
            Version version;
            BackupManager.GetVersion(BackupManager.ImportPast("travelers.json"), out travelerText, out version);

            List<string> travelerArray = new StringStream(travelerText).ParseJSONarray();
            Server.Write("\r{0}", "Loading travelers from backup...");
            foreach (string travelerJSON in travelerArray)
            {
                Traveler traveler = ImportTraveler(travelerJSON, version);
                // add this traveler to the master list if it is not null
                if (traveler != null)
                {
                    // add this traveler to the imported list
                    m_importedFromPast.Add(traveler);
                    // add this traveler to the list
                    m_travelers.Add(traveler);
                }
            }
            Server.Write("\r{0}", "Loading travelers from backup...Finished" + Environment.NewLine);
            
        }
        public void EnterProduction()
        {
            foreach (Traveler traveler in m_importedFromPast)
            {
                // push this traveler into production
                if (traveler.State == GlobalItemState.PreProcess && traveler.Station != StationClass.GetStation("Start"))
                {
                    traveler.EnterProduction(this as ITravelerManager);
                }
            }
        }
        public void Backup()
        {
            BackupManager.Backup("travelers.json", m_travelers.Stringify<Traveler>(false,true));
        }
        #endregion
        //----------------------------------
        #region ITravelerManager

        public Traveler FindTraveler(int ID)
        {
            return m_travelers.Find(x => x.ID == ID);
        }
        public bool FindTraveler(int ID, out Traveler traveler)
        {
            traveler = m_travelers.Find(x => x.ID == ID);
            return traveler != null;
        }
        public void RemoveTravelers(List<Traveler> travelers)
        {
            foreach (Traveler traveler in travelers)
            {
                RemoveTraveler(traveler,false);
            }
            Server.OrderManager.Backup();
            Server.TravelerManager.Backup();
        }
        public void RemoveTraveler(Traveler traveler, bool backup = true)
        {
            // remove itself from order items
            //foreach (Order parentOrder in traveler.ParentOrders)
            //{
            //    foreach (OrderItem item in parentOrder.FindItems(traveler.ID))
            //    {
            //        item.ChildTraveler = -1;
            //    }
            //}
            foreach (Traveler child in traveler.ChildTravelers)
            {
                traveler.ChildTravelers.Remove(child);
                traveler.ChildIDs.Remove(child.ID);
                // recursively remove children
                RemoveTraveler(child);
            }
            // remove itself from parents
            foreach (Traveler parent in traveler.ParentTravelers)
            {
                parent.ChildTravelers.Remove(traveler);
            }
            // finally... remove THIS traveler
            m_travelers.Remove(traveler);
            Server.OrderManager.ReleaseTraveler(traveler, backup);
            if (backup) Backup();
        }
        public Traveler AddTraveler(string itemCode, int quantity)
        {
            OdbcConnection MAS = Server.GetMasConnection();
            // create a new traveler from the itemcode and quantity
            Traveler newTraveler = (Traveler.IsTable(itemCode) ? (Traveler)new Table(itemCode, quantity) : null /*(Traveler)new Chair(item.ItemCode, quantity)*/);

            newTraveler.ImportInfo(this as ITravelerManager, m_orderManager, MAS);
            m_travelers.Add(newTraveler);
            OnTravelersChanged(m_travelers);
            return newTraveler;
        }
        public List<Traveler> GetTravelers
        {
            get
            {
                return m_travelers;
            }
        }
        public void ClearStartQueue()
        {
            foreach (Traveler traveler in new List<Traveler>(GetTravelers.Where(t => t.State == GlobalItemState.PreProcess && t.Station == StationClass.GetStation("Start"))))
            {
                RemoveTraveler(traveler,false);
            }
            OnTravelersChanged();
        }
        //public void AdvanceTravelerItem(int travelerID, ushort itemID)
        //{
        //    FindTraveler(travelerID).AdvanceItem(itemID);
        //}
        #endregion
        //----------------------------------
        #region IOperator
        //public void ScrapTravelerItem(int travelerID, ushort itemID)
        //{
        //    FindTraveler(travelerID).ScrapItem(itemID);
        //}
        // has to know which station this is being completed from
        // TODO: change param list to take event, and construct the even from the client
        //public ClientMessage AddTravelerEvent(ProcessEvent itemEvent, Traveler traveler, TravelerItem item = null)
        //{
        //    ClientMessage returnMessage = new ClientMessage();
        //    try
        //    {
        //        bool newItem = false;
        //        if (item == null)
        //        {
        //            newItem = true;
        //            // create a new item
        //            item = traveler.AddItem(itemEvent.Station);
        //        }
                
        //        item.History.Add(itemEvent);
        //        // print labels
        //        if (itemEvent.Process == ProcessType.Scrapped)
        //        {
        //            //=================
        //            // SCRAPPED
        //            //=================
        //            traveler.ScrapItem(item.ID);
        //            returnMessage = new ClientMessage("Info", traveler.PrintLabel(item.ID, LabelType.Scrap) + " for item: " + traveler.PrintSequenceID(item));
        //            item.Station = StationClass.GetStation("Scrapped");

                    
        //        } else if (newItem)
        //        {
        //            //=================
        //            // NEW
        //            //=================
        //            LabelType labelType = LabelType.Tracking;
        //            if (traveler is Chair)
        //            {
        //                labelType = LabelType.Chair;
        //            } else if (traveler is Box)
        //            {
        //                labelType = LabelType.Box;
        //            }
        //            returnMessage = new ClientMessage("Info", traveler.PrintLabel(item.ID, labelType) + " for item: " + traveler.PrintSequenceID(item));
        //        }
        //        if (itemEvent.Process == ProcessType.Completed && traveler.GetNextStation(item.ID) == StationClass.GetStation("Finished"))
        //        {
        //            //=================
        //            // FINISHED
        //            //=================
        //            traveler.FinishItem(item.ID);
                    
        //            // assign this item to the order that ships soonest
        //            //AssignOrder(traveler, item);


        //            // Pack tracking label must be printed
        //            if (traveler is Table)
        //            {
                        
        //                //returnMessage = new ClientMessage("Info", traveler.PrintLabel(item.ID, LabelType.Pack, 2) + " for item: " + traveler.ID.ToString("D6") + '-' + item.ID);
        //                //returnMessage = new ClientMessage("Info", traveler.PrintLabel(item.ID, LabelType.Table) + " for item: " + traveler.ID.ToString("D6") + '-' + item.ID);
        //            }
        //        }
        //        OnTravelersChanged(new List<Traveler>() { traveler });
        //    } catch (Exception ex)
        //    {
        //        Server.WriteLine("Problem completing travelerItem: " + ex.Message + "stack trace: " + ex.StackTrace);
        //        returnMessage = new ClientMessage("Info", "Problem completing travelerItem");
        //    }
        //    return returnMessage;
        //}
        // has to know which station this is being submitted from
        public ClientMessage SubmitTraveler(Traveler traveler, StationClass station)
        {
            if (traveler != null && station != null)
            {
                //traveler.Advance(station, this);
                OnTravelersChanged(new List<Traveler>() { traveler });
            }
            return new ClientMessage();
        }
        #endregion
        //----------------------------------
        #region ISupervisor
        public string MoveTravelerStart(string json)
        {
            ClientMessage returnMessage = new ClientMessage();
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                List<string> travelerIDs = new List<string>();
                if (obj.ContainsKey("travelerIDs")) travelerIDs = new StringStream(obj["travelerIDs"]).ParseJSONarray();
                if (obj.ContainsKey("travelerID")) travelerIDs.Add(obj["travelerID"]);
                foreach (string ID in travelerIDs)
                {
                    Traveler traveler = FindTraveler(Convert.ToInt32(ID));
                    if (traveler != null && StationClass.GetStation(obj["value"]) != null)
                    {
                        traveler.Station = StationClass.GetStation(obj["value"]);
                    }
                }
                OnTravelersChanged(GetTravelers);
            } catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
                returnMessage = new ClientMessage("Info","error");
            }
            return returnMessage.ToString();
        }

        public ClientMessage LoadTravelerJSON(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Traveler traveler = FindTraveler(Convert.ToInt32(obj["travelerID"]));
                return new ClientMessage("LoadTravelerJSON", traveler.ExportHuman());
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error");
            }
        }
        //public ClientMessage LoadTraveler(string json)
        //{
        //    ClientMessage returnMessage = new ClientMessage();
        //    try
        //    {
        //        Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
        //        Traveler traveler = FindTraveler(Convert.ToInt32(obj["travelerID"]));
        //        if (traveler != null)
        //        {
        //            returnMessage = new ClientMessage("LoadTraveler", traveler.Export());
        //        } else
        //        {
        //            returnMessage = new ClientMessage("Info", "\"Invalid traveler number\"");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
        //        returnMessage = new ClientMessage("Info", "error");
        //    }
        //    return returnMessage;
        //}
        //public ClientMessage LoadTravelerAt(string json)
        //{
        //    ClientMessage returnMessage = new ClientMessage();
        //    try
        //    {
        //        Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
        //        Traveler traveler = FindTraveler(Convert.ToInt32(obj["travelerID"]));
        //        if (traveler != null)
        //        {
        //            returnMessage = new ClientMessage("LoadTravelerAt", traveler.Export("OperatorClient", StationClass.GetStation(obj["station"])));
        //        }
        //        else
        //        {
        //            returnMessage = new ClientMessage("Info", "Invalid traveler number");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
        //        returnMessage = new ClientMessage("Info", "error");
        //    }
        //    return returnMessage;
        //}
        public void LinkTravelers()
        {
            foreach(Traveler traveler in m_travelers)
            {
                // parents
                foreach (int parentID in traveler.ParentIDs)
                {
                    traveler.ParentTravelers.Add(FindTraveler(parentID));
                }
                // children
                foreach (int childID in traveler.ChildIDs)
                {
                    traveler.ChildTravelers.Add(FindTraveler(childID));
                }
            }
        }
        public ClientMessage LoadItem(string json)
        {
            ClientMessage returnMessage = new ClientMessage();
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                Traveler traveler = FindTraveler(Convert.ToInt32(obj["travelerID"]));
                if (traveler != null)
                {
                    TravelerItem item = traveler.FindItem(Convert.ToUInt16(obj["itemID"]));
                    if (item != null)
                    {
                        returnMessage = new ClientMessage("LoadItem", item.ToString());
                    } else
                    {
                        returnMessage = new ClientMessage("Info", "\"Invalid traveler item number\"");
                    }
                    
                } else
                {
                    returnMessage = new ClientMessage("Info", "\"Invalid traveler number\"");
                }
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
                returnMessage = new ClientMessage("Info", "error");
            }
            return returnMessage;
        }
        public ClientMessage CreateSummary(string json)
        {
            ClientMessage returnMessage;
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                //Summary summary = new Summary(this as ITravelerManager);
                string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                Summary summary = null;
                if (obj["from"] != "" && obj["to"] != "")
                {
                    DateTime from = (obj["from"] != "" ? DateTime.Parse(obj["from"]) : BackupManager.GetMostRecent());
                    DateTime to = (obj["to"] != "" ? DateTime.Parse(obj["to"]) : DateTime.Today.Date);
                    summary = new Summary(from, to, obj["type"], (SummarySort)Enum.Parse(typeof(SummarySort), obj["sort"]));
                } else
                {
                    summary = new Summary(this as ITravelerManager,obj["type"], (SummarySort)Enum.Parse(typeof(SummarySort), obj["sort"]));
                }
                returnMessage = new ClientMessage("CreateSummary", summary.ToString());
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
                returnMessage = new ClientMessage("Info", "error");
            }
            return returnMessage;
        }
        //public ClientMessage DisintegrateTraveler(string json)
        //{
        //    ClientMessage returnMessage;
        //    try
        //    {
        //        Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
        //        List<string> travelerIDs = new List<string>();
        //        if (obj.ContainsKey("travelerIDs")) travelerIDs = new StringStream(obj["travelerIDs"]).ParseJSONarray();
        //        if (obj.ContainsKey("travelerID")) travelerIDs.Add(obj["travelerID"]);
        //        List<string> success = new List<string>();
        //        List<string> failure = new List<string>();
        //        foreach (string ID in travelerIDs)
        //        {
        //            Traveler traveler = FindTraveler(Convert.ToInt32(ID));
        //            if (traveler != null && traveler.Items.Count == 0)
        //            {
        //                m_travelers.Remove(traveler);
        //                Server.OrderManager.ReleaseTraveler(traveler);
        //                OnTravelersChanged(m_travelers);
        //                success.Add(ID);
        //            }
        //            else
        //            {
        //                failure.Add(ID);
        //            }
        //        }
        //        returnMessage = new ClientMessage("Info", (success.Count > 0 ? "Disintegrated: " + success.Stringify<string>(false) : "") + (failure.Count > 0 ? "<br>Failed to disintegrate: " + failure.Stringify<string>(false) : ""));
        //    }
        //    catch (Exception ex)
        //    {
        //        Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
        //        returnMessage = new ClientMessage("Info", "error");
        //    }
        //    return returnMessage;
        //}
        public ClientMessage EnterProduction(string json)
        {
            ClientMessage returnMessage = new ClientMessage();
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                List<string> travelerIDs = new List<string>();
                if (obj.ContainsKey("travelerIDs")) travelerIDs = new StringStream(obj["travelerIDs"]).ParseJSONarray();
                if (obj.ContainsKey("travelerID")) travelerIDs.Add(obj["travelerID"]);
                foreach (string ID in travelerIDs)
                {
                    Traveler traveler = FindTraveler(Convert.ToInt32(ID));
                    if (traveler != null)
                    {
                        traveler.EnterProduction(this as ITravelerManager);
                    }
                }
                OnTravelersChanged(GetTravelers);
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
                returnMessage = new ClientMessage("Info", "error");
            }
            return returnMessage;
        }
        public ClientMessage DownloadSummary(string json)
        {
            ClientMessage returnMessage = new ClientMessage();
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                Summary summary = new Summary(this as ITravelerManager, obj["type"], (SummarySort)Enum.Parse(typeof(SummarySort), obj["sort"]));
                string downloadLocation = summary.MakeCSV();
                returnMessage = new ClientMessage("Redirect", downloadLocation.Quotate());
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
                returnMessage = new ClientMessage("Info", "error");
            }
            return returnMessage;
        }

        public ClientMessage TravelerForm(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                //Type type = Type.GetType(obj["type"]);
                Type type = Type.GetType("Efficient_Automatic_Traveler_System.Table");
                Traveler traveler = (Traveler)Activator.CreateInstance(type);
                Form form = traveler.CreateForm();
                form.Source = "TravelerManager";
                return new ClientMessage("TravelerForm", form.ToString());
            } catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "error in TravelerManager.TravelerForm");
            }
        }
        public async Task<ClientMessage> NewTraveler(string json)
        {
            try
            {
                OdbcConnection MAS = Server.GetMasConnection();
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Form form = new Form(json);
                Type type = Type.GetType("Efficient_Automatic_Traveler_System." + form.Name);
                Traveler traveler = traveler = (Traveler)Activator.CreateInstance(type, form);
                traveler.ImportInfo(this as ITravelerManager, m_orderManager, MAS);
                m_travelers.Add(traveler);
                OnTravelersChanged(m_travelers);
                return new ClientMessage("Info","Success!");
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "error");
            }
        }
        #endregion
        //----------------------------------
        #region Private methods

        // Gets the total quantity ordered, compensated by what is in stock
        private int QuantityNeeded(Traveler traveler)
        {
            int qtyNeeded = 0;
            foreach (string orderNo in traveler.ParentOrderNums)
            {
                Order order = m_orderManager.FindOrder(orderNo);
                if (order != null)
                {
                    foreach (OrderItem item in order.Items)
                    {
                        if (item.ChildTraveler == traveler.ID)
                        {
                            qtyNeeded += Math.Max(0, item.QtyOrdered - item.QtyOnHand);
                        }
                    }
                }
            }
            return qtyNeeded;
        }
        // updates the quantity of a traveler and all its children
        //private void UpdateQuantity(Traveler traveler)
        //{
        //    // 1.) compensate highest level traveler with inventory
        //    // if it has parent orders and hasn't started, the quantity can change
        //    if (traveler.LastStation == Traveler.GetStation("Start") && traveler.ParentOrders.Count > 0)
        //    {
        //        traveler.Quantity = QuantityNeeded(traveler);
        //    }
        //    // 2.) adjust children quantities
        //    if (traveler.Children.Count > 0)
        //    {
        //        int qtyNeeded = Math.Max(0, QuantityNeeded(traveler) - traveler.Quantity);
        //        List<Traveler> started = new List<Traveler>();
        //        List<Traveler> notStarted = new List<Traveler>();
        //        foreach (int childID in traveler.Children)
        //        {
        //            Traveler child = FindTraveler(childID);
        //            if (child != null)
        //            {
        //                // update children of child
        //                // can only change quantity if this child hasn't started
        //                if (child.LastStation == Traveler.GetStation("Start"))
        //                {
        //                    notStarted.Add(child);
        //                }
        //                else
        //                {
        //                    started.Add(child);
        //                    qtyNeeded -= child.Quantity;
        //                }
        //            }
        //        }
        //        foreach (Traveler child in notStarted)
        //        {
        //            if (qtyNeeded == 0)
        //            {
        //                m_travelers.Remove(child); // don't need this anymore
        //                traveler.Children.RemoveAll(x => x == child.ID);
        //            }
        //            else
        //            {
        //                child.Quantity = qtyNeeded;
        //                qtyNeeded = 0;
        //            }
        //        }
        //    }
        //}


        // Imports travelers that have been stored
        
        private bool IsBackPanel(string s)
        {
            if (s.Substring(0, 2) == "32")
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public void OnTravelersChanged(List<Traveler> travelers = null)
        {
            // Update the travelers.json file with all the current travelers
            Backup();
            // fire the event
            TravelersChanged(travelers != null ? travelers : m_travelers);
        }
        public void OnTravelersChanged(Traveler traveler)
        {
            OnTravelersChanged(new List<Traveler>() { traveler });
        }
        private Traveler ImportTraveler(string json,Version version)
        {
            Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
            Traveler traveler = null;
            if (obj["type"] != "")
            {
                Type type = Type.GetType(Server.Assembly + obj["type"]);
                traveler = (Traveler)Activator.CreateInstance(type, json, version);
            }
            return traveler;
        }
        #endregion
        //----------------------------------
        #region Private member variables
        private List<Traveler> m_travelers;
        private List<Traveler> m_importedFromPast;
        private IOrderManager m_orderManager;

        public event TravelersChangedSubscriber TravelersChanged; // plural
        //public event TravelerChangedSubscriber TravelerChanged; // singular
        #endregion
    }
}
