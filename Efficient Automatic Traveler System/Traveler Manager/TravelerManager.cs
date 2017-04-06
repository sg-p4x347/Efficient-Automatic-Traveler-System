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

namespace Efficient_Automatic_Traveler_System
{
    // Class: Used to generate and store the digital "travelers" that are used throughout the system
    // Developer: Gage Coates
    // Date started: 1/25/17

    interface ITravelerManager
    {
        //void CreateScrapChild(Traveler parent, int qtyScrapped);
        //Traveler CreateCompletedChild(Traveler parent, int qtyMade, double time);
        Traveler FindTraveler(int ID);
        void AdvanceTravelerItem(int travelerID, ushort itemID);
        void ScrapTravelerItem(int travelerID, ushort itemID);
        void RemoveTraveler(int travelerID);
        List<Traveler> GetTravelers
        {
            get;
        }
        
    }
    interface IOperator : ITravelerManager
    {
        string AddTravelerEvent(string json);
        string SubmitTraveler(string json);
    }
    interface ISupervisor : ITravelerManager
    {
        string MoveTravelerStart(string json);
        string LoadTraveler(string json);
        string LoadTravelerAt(string json);
        string LoadItem(string json);
        string CreateSummary(string json);
        string DisintegrateTraveler(string json);
        string EnterProduction(string json);
        string DownloadSummary(string json);
    }
    internal delegate void TravelersChangedSubscriber(List<Traveler> travelers);
    class TravelerManager : IManager, ITravelerManager, IOperator, ISupervisor
    {
        #region Public methods
        public TravelerManager(IOrderManager orderManager, string workingDirectory)
        {
            TravelersChanged = delegate { };
            m_travelers = new List<Traveler>();
            m_orderManager = orderManager;
            m_workingDirectory = workingDirectory;


        }
        public void CompileTravelers()
        {
            // import stored travelers
            Import();

            int index = 0;
            foreach (Order order in m_orderManager.GetOrders)
            {
                foreach (OrderItem item in order.Items)
                {
                    // only make a traveler if this one has no child traveler already (-1 signifies no child traveler)
                    if (item.ChildTraveler < 0 && (Traveler.IsTable(item.ItemCode) || Traveler.IsChair(item.ItemCode)))
                    {
                        Server.Write("\r{0}%", "Compiling Travelers..." + Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(m_orderManager.GetOrders.Count)) * 100));

                        // search for existing traveler
                        // can only combine if same itemCode, hasn't started, and has no parents
                        Traveler traveler = m_travelers.Find(x => x.ItemCode == item.ItemCode);
                        if (traveler != null)
                        {
                            // add to existing traveler
                            traveler.Quantity += item.QtyOrdered;

                            // RELATIONAL =============================================================
                            item.ChildTraveler = traveler.ID;
                            traveler.ParentOrders.Add(order.SalesOrderNo);
                            //=========================================================================
                        }
                        else
                        {
                            // create a new traveler from the new item
                            Traveler newTraveler = (Traveler.IsTable(item.ItemCode) ? (Traveler)new Table(item.ItemCode, item.QtyOrdered) : (Traveler)new Chair(item.ItemCode, item.QtyOrdered));

                            // RELATIONAL =============================================================
                            item.ChildTraveler = newTraveler.ID;
                            newTraveler.ParentOrders.Add(order.SalesOrderNo);
                            //=========================================================================

                            // add the new traveler to the list
                            m_travelers.Add(newTraveler);
                        }
                    }
                }
                index++;
            }
            Server.Write("\r{0}", "Compiling Travelers...Finished\n");
        }
        public void ImportTravelerInfo(IOrderManager orderManager, ref OdbcConnection MAS)
        {
            int index = 0;
            foreach (Traveler traveler in m_travelers)
            {
                
                traveler.ImportPart(orderManager, ref MAS);
                index++;
                Server.Write("\r{0}%", "Gathering Info..." + Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(m_travelers.Count)) * 100));
            }
            Server.Write("\r{0}", "Gathering Info...Finished\n");
            // travelers have changed
            OnTravelersChanged(m_travelers);
        }
        #endregion
        //----------------------------------
        #region IManager
        public void Import(DateTime? date = null)
        {

            m_travelers.Clear();
            if (BackupManager.CurrentBackupExists() || date != null)
            {
                List<string> travelerArray = (new StringStream(BackupManager.Import("travelers.json", date))).ParseJSONarray();
                Server.Write("\r{0}", "Loading travelers from backup...");
                foreach (string travelerJSON in travelerArray)
                {
                    Traveler traveler = ImportTraveler(travelerJSON);
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
        }
        public void ImportPast()
        {
            m_travelers.Clear();
            List<string> travelerArray = (new StringStream(BackupManager.Import("travelers.json"))).ParseJSONarray();
            Server.Write("\r{0}", "Loading travelers from backup...");
            foreach (string travelerJSON in travelerArray)
            {
                Traveler traveler = ImportTraveler(travelerJSON);
                // add this traveler to the master list if it is not complete
                if (traveler != null && traveler.State != ItemState.PostProcess)
                {
                    // push this traveler into production
                    if (traveler.State == ItemState.PreProcess && traveler.Station != StationClass.GetStation("Start"))
                    {
                        traveler.EnterProduction();
                    }
                    // add this traveler to the list
                    m_travelers.Add(traveler);
                }
            }
            Server.Write("\r{0}", "Loading travelers from backup...Finished" + Environment.NewLine);
        }
        public void Backup()
        {
            BackupManager.Backup("travelers.json", m_travelers.Stringify<Traveler>());
        }
        #endregion
        //----------------------------------
        #region ITravelerManager

        public Traveler FindTraveler(int ID)
        {
            return m_travelers.Find(x => x.ID == ID);
        }
        public void RemoveTraveler(int ID)
        {
            // remove itself from order items
            foreach (string orderNo in FindTraveler(ID).ParentOrders)
            {
                foreach (OrderItem item in m_orderManager.FindOrder(orderNo).FindItems(ID))
                {
                    item.ChildTraveler = -1;
                }
            }
            //// remove itself from parents
            //foreach (int parentID in traveler.Parents)
            //{
            //    FindTraveler(parentID).Children.Remove(traveler.ID);
            //}
            //// recursively remove children
            //foreach (int childID in traveler.Children)
            //{
            //    RemoveTraveler(FindTraveler(childID));
            //}
            // finally... remove THIS traveler
            m_travelers.RemoveAll(x => x.ID == ID);
        }
        public List<Traveler> GetTravelers
        {
            get
            {
                return m_travelers;
            }
        }

        public void AdvanceTravelerItem(int travelerID, ushort itemID)
        {
            FindTraveler(travelerID).AdvanceItem(itemID);
        }
        #endregion
        //----------------------------------
        #region IOperator
        public void ScrapTravelerItem(int travelerID, ushort itemID)
        {
            FindTraveler(travelerID).ScrapItem(itemID);
        }
        // has to know which station this is being completed from
        public string AddTravelerEvent(string json)
        {
            ClientMessage returnMessage = new ClientMessage("void", "void");
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                Traveler traveler = FindTraveler(Convert.ToInt32(obj["travelerID"]));
                EventType eventType = (EventType)Enum.Parse(typeof(EventType), obj["eventType"]);
                Event itemEvent = new Event(eventType, Convert.ToDouble(obj["time"]), StationClass.GetStation(obj["station"]));
                TravelerItem item;
                bool newItem = false;
                if (obj["itemID"] == "undefined")
                {
                    newItem = true;
                    // create a new item
                    item = traveler.AddItem(StationClass.GetStation(obj["station"]));
                } else
                {
                    // change existing item
                    item = traveler.FindItem(Convert.ToUInt16(obj["itemID"]));
                }
                
                item.History.Add(itemEvent);
                // print labels
                if (itemEvent.type == EventType.Scrapped)
                {
                    //=================
                    // SCRAPPED
                    //=================
                    traveler.ScrapItem(item.ID);
                    returnMessage = new ClientMessage("Info", traveler.PrintLabel(item.ID, LabelType.Scrap) + " for item: " + traveler.ID.ToString("D6") + '-' + item.ID);

                } else if (newItem)
                {
                    //=================
                    // NEW
                    //=================
                    returnMessage = new ClientMessage("Info", traveler.PrintLabel(item.ID, LabelType.Tracking) + " for item: " + traveler.ID.ToString("D6") + '-' + item.ID);
                } else if (itemEvent.type == EventType.Completed && traveler.GetNextStation(item.ID) == StationClass.GetStation("Finished"))
                {
                    //=================
                    // FINISHED
                    //=================
                    traveler.FinishItem(item.ID);
                    // assign this item to the order that ships soonest
                    AssignOrder(traveler, item);
                    // Pack tracking label must be printed
                    returnMessage = new ClientMessage("Info", traveler.PrintLabel(item.ID, LabelType.Pack, 2) + " for item: " + traveler.ID.ToString("D6") + '-' + item.ID);
                    returnMessage = new ClientMessage("Info", traveler.PrintLabel(item.ID, LabelType.Table) + " for item: " + traveler.ID.ToString("D6") + '-' + item.ID);
                }
                OnTravelersChanged(new List<Traveler>() { traveler });
            } catch (Exception ex)
            {
                Server.WriteLine("Problem completing travelerItem: " + ex.Message + "stack trace: " + ex.StackTrace);
                returnMessage = new ClientMessage("Info", "Problem completing travelerItem");
            }
            return returnMessage.ToString();
        }
        // has to know which station this is being submitted from
        public string SubmitTraveler(string json)
        {
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                Traveler traveler = FindTraveler(Convert.ToInt32(obj["travelerID"]));
                traveler.Advance(StationClass.GetStation(obj["station"]));
                OnTravelersChanged(new List<Traveler>() { traveler });
            }
            catch (Exception ex)
            {
                Server.WriteLine("Problem submitting traveler: " + ex.Message + "stack trace: " + ex.StackTrace);
            }
            return "";
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
                List<string> travelerIDs = (new StringStream(obj["travelerIDs"])).ParseJSONarray();
                foreach (string ID in travelerIDs)
                {
                    Traveler traveler = FindTraveler(Convert.ToInt32(ID));
                    if (traveler != null)
                    {
                        traveler.Station = StationClass.GetStation(obj["station"]);
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
        public string LoadTraveler(string json)
        {
            ClientMessage returnMessage;
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                Traveler traveler = FindTraveler(Convert.ToInt32(obj["travelerID"]));
                if (traveler != null)
                {
                    returnMessage = new ClientMessage("LoadTraveler", traveler.ExportHuman());
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
            return returnMessage.ToString();
        }
        public string LoadTravelerAt(string json)
        {
            ClientMessage returnMessage;
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                Traveler traveler = FindTraveler(Convert.ToInt32(obj["travelerID"]));
                if (traveler != null)
                {
                    returnMessage = new ClientMessage("LoadTravelerAt", traveler.Export("OperatorClient", StationClass.GetStation(obj["station"])));
                }
                else
                {
                    returnMessage = new ClientMessage("Info", "\"Invalid traveler number\"");
                }
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
                returnMessage = new ClientMessage("Info", "error");
            }
            return returnMessage.ToString();
        }
        public string LoadItem(string json)
        {
            ClientMessage returnMessage;
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
            return returnMessage.ToString();
        }
        public string CreateSummary(string json)
        {
            ClientMessage returnMessage;
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                //Summary summary = new Summary(this as ITravelerManager);
                string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                //Summary summary = new Summary(Path.Combine(exeDir,"backup\\03-24-2017"), m_workingDirectory, (SummarySort)Enum.Parse(typeof(SummarySort), obj["sort"]));
                //returnMessage = new ClientMessage("CreateSummary", summary.ToString());
                returnMessage = new ClientMessage("Info", "error");
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
                returnMessage = new ClientMessage("Info", "error");
            }
            return returnMessage.ToString();
        }
        public string DisintegrateTraveler(string json)
        {
            ClientMessage returnMessage;
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                List<string> travelerIDs = (new StringStream(obj["travelerIDs"])).ParseJSONarray();
                List<string> success = new List<string>();
                List<string> failure = new List<string>();
                foreach (string ID in travelerIDs)
                {
                    Traveler traveler = FindTraveler(Convert.ToInt32(ID));
                    if (traveler != null && traveler.Items.Count == 0)
                    {
                        m_travelers.Remove(traveler);
                        m_orderManager.ReleaseTraveler(traveler);
                        OnTravelersChanged(m_travelers);
                        success.Add(ID);
                    }
                    else
                    {
                        failure.Add(ID);
                    }
                }
                returnMessage = new ClientMessage("Info", (success.Count > 0 ? "Disintegrated: " + success.Stringify<string>(false) : "") + (failure.Count > 0 ? "<br>Failed to disintegrate: " + failure.Stringify<string>(false) : ""));
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
                returnMessage = new ClientMessage("Info", "error");
            }
            return returnMessage.ToString();
        }
        public string EnterProduction(string json)
        {
            ClientMessage returnMessage = new ClientMessage();
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                List<string> travelerIDs = (new StringStream(obj["travelerIDs"])).ParseJSONarray();
                foreach (string ID in travelerIDs)
                {
                    Traveler traveler = FindTraveler(Convert.ToInt32(ID));
                    if (traveler != null)
                    {
                        traveler.EnterProduction();
                    }
                }
                OnTravelersChanged(GetTravelers);
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
                returnMessage = new ClientMessage("Info", "error");
            }
            return returnMessage.ToString();
        }
        public string DownloadSummary(string json)
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
            return returnMessage.ToString();
        }
        #endregion
        //----------------------------------
        #region Private methods

        // Gets the total quantity ordered, compensated by what is in stock
        private int QuantityNeeded(Traveler traveler)
        {
            int qtyNeeded = 0;
            foreach (string orderNo in traveler.ParentOrders)
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
        private void OnTravelersChanged(List<Traveler> travelers)
        {
            // Update the travelers.json file with all the current travelers
            Backup();
            // fire the event
            TravelersChanged(travelers);
        }

        private void AssignOrder(Traveler traveler, TravelerItem item)
        {
            List<Order> parentOrders = new List<Order>();
            foreach (string orderNo in traveler.ParentOrders)
            {
                parentOrders.Add(m_orderManager.FindOrder(orderNo));
            }
            parentOrders.Sort((a, b) => a.ShipDate.CompareTo(b.ShipDate)); // sort in ascending order (soonest first)
            foreach (Order order in parentOrders)
            {
                List<OrderItem> orderItems = order.FindItems(traveler.ID); // the items that apply to this traveler

                // If there are less items assigned to that order than what was ordered (takes into account multiple order items that match the traveler)
                if (traveler.Items.Where(x => x.Order == order.SalesOrderNo).Count() < orderItems.Sum(x => x.QtyOrdered))
                {
                    // assign this order to the item
                    item.Order = order.SalesOrderNo;
                }
            }
        }
        private Traveler ImportTraveler(string json)
        {
            Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
            Traveler traveler = null;
            if (obj["type"] != "")
            {
                Type type = Type.GetType(obj["type"]);
                traveler = (Traveler)Activator.CreateInstance(type, json);
            }
            return traveler;
        }
        #endregion
        //----------------------------------
        #region Private member variables
        private List<Traveler> m_travelers;
        private IOrderManager m_orderManager;
        private string m_workingDirectory;

        public event TravelersChangedSubscriber TravelersChanged; // plural
        //public event TravelerChangedSubscriber TravelerChanged; // singular
        #endregion
    }
}
