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
    interface IClientToTraveler
    {
        void AddTravelerEvent(string json);
        void SubmitTraveler(string json);
        

    }
    public delegate void TravelersChangedSubscriber();
    class TravelerManager : ITravelerManager, IClientToTraveler
    {
        #region Public methods
        public TravelerManager(IOrderManager orderManager)
        {
            TravelersChanged = delegate { };
            m_travelers = new List<Traveler>();
            m_orderManager = orderManager;
            // set up the station list
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            System.IO.StreamReader stationsFile = new System.IO.StreamReader(System.IO.Path.Combine(exeDir, "stations.txt"));
            string line;
            for  ( int i = 0;  (line = stationsFile.ReadLine()) != null && line != ""; i++)
            {
                Traveler.Stations.Add(line, i);
            }
        }
        public void CompileTravelers(ref List<Order> newOrders)
        {
            // first import stored travelers
            ImportStoredTravelers();

            int index = 0;
            foreach (Order order in newOrders)
            {
                foreach (OrderItem item in order.Items)
                {
                    // only make a traveler if this one has no child traveler already (-1 signifies no child traveler)
                    if (item.ChildTraveler < 0 && (Traveler.IsTable(item.ItemCode) || Traveler.IsChair(item.ItemCode)))
                    {
                        Console.Write("\r{0}%   ", "Compiling Travelers..." + Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(newOrders.Count)) * 100));

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

                            // start the new traveler's journey
                            newTraveler.MoveTo(Traveler.GetStation("Start"));
                            // add the new traveler to the list
                            m_travelers.Add(newTraveler);
                        }
                    }
                }
                index++;
            }
            Console.Write("\r{0}   ", "Compiling Tables...Finished\n");

            BackupTravelers();
        }
        public void ImportTravelerInfo(IOrderManager orderManager, ref OdbcConnection MAS)
        {
            foreach (Traveler traveler in m_travelers)
            {
                traveler.ImportPart(orderManager, ref MAS);
            }
        }
        
        public void HandleTravelersChanged()
        {
            OnTravelersChanged();
        }
        #endregion
        //----------------------------------
        #region Interface
        // Creates a child of the parent, returns the child if the quantity was not 0
        //public void CreateScrapChild(Traveler parent, int qtyScrapped)
        //{
        //    Traveler scrapped = parent.Clone();
        //    scrapped.Quantity = qtyScrapped;
        //    parent.Quantity -= qtyScrapped;
        //    scrapped.Start();
        //    scrapped.History.Add(new Event(TravelerEvent.Scrapped, scrapped.Quantity, parent.Station));
        //    m_travelers.Add(scrapped);

        //    // compensate for inventory
        //    UpdateQuantity(parent);
        //}
        //public Traveler CreateCompletedChild(Traveler parent, int qtyMade, double time)
        //{
        //    Traveler made = (Traveler)parent.Clone();

        //    made.Quantity = qtyMade;
        //    parent.Quantity -= qtyMade;
        //    made.NextStation = parent.NextStation;
        //    made.History.Add(new Event(TravelerEvent.Completed, made.Quantity, parent.Station, time));
        //    m_travelers.Add(made);
        //    AdvanceTraveler(made);
            

        //    return made;
        //}
        
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
        //public void AdvanceTraveler(Traveler traveler)
        //{
        //    traveler.Station = traveler.NextStation;
        //    traveler.Advance();
        //    int ancestor = FindAncestor(traveler).ID;
        //    // check to see if this traveler can re-combine with family
        //    Traveler toRemove = null;
        //    foreach (Traveler relative in m_travelers)
        //    {
        //        // if they have a common ancestor
        //        if (relative.Station == traveler.Station && relative.ID != traveler.ID && (FindAncestor(relative).ID == ancestor))
        //        {
        //            if (relative.ID < traveler.ID)
        //            {
        //                // the relative is older if the ID is less
        //                relative.Quantity += traveler.Quantity;
        //                Event e = new Event(TravelerEvent.Merged, traveler.Quantity, traveler.LastStation);
        //                e.message = "Traveler [" + traveler.ID.ToString("D6") + "] has merged with this traveler. Please combine it's parts with this traveler's parts and destroy it's label.";
        //                relative.History.Add(e);
        //                toRemove = traveler;
        //            } else
        //            {
        //                // the traveler is older than the relative
        //                traveler.Quantity += relative.Quantity;
        //                Event e = new Event(TravelerEvent.Merged, relative.Quantity, relative.LastStation);
        //                e.message = "Traveler [" + relative.ID.ToString("D6") + "] has merged with this traveler. Please combine its parts with this traveler's parts and destroy its label.";
        //                traveler.History.Add(e);
        //                toRemove = relative;
        //            }
        //        }
        //    }
        //    if (toRemove != null)
        //    {
        //        RemoveTraveler(toRemove);
        //    }
        //}
        //public Traveler FindAncestor(Traveler child)
        //{
        //    foreach (int parentID in child.Parents)
        //    {
        //        return FindAncestor(FindTraveler(parentID));
        //    }
        //    return child;
        //}
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

        public void ScrapTravelerItem(int travelerID, ushort itemID)
        {
            FindTraveler(travelerID).ScrapItem(itemID);
        }
        // has to know which station this is being completed from
        public void AddTravelerEvent(string json)
        {
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                TravelerItem item = FindTraveler(Convert.ToInt32(obj["travelerID"])).FindItem(Convert.ToUInt16(obj["itemID"]));
                item.History.Add(new Event((TravelerEvent)Enum.Parse(typeof(TravelerEvent), obj["eventType"]), Convert.ToDouble(obj["time"]), Traveler.GetStation(obj["station"])));
            } catch (Exception ex)
            {
                Server.WriteLine("Problem completing travelerItem: " + ex.Message + "stack trace: " + ex.StackTrace);
            }
        }
        // has to know which station this is being submitted from
        public void SubmitTraveler(string json)
        {
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                Traveler traveler = FindTraveler(Convert.ToInt32(obj["travelerID"]));
                traveler.Advance(Traveler.GetStation(obj["station"]));
            }
            catch (Exception ex)
            {
                Server.WriteLine("Problem submitting traveler: " + ex.Message + "stack trace: " + ex.StackTrace);
            }
        }

        #endregion
        //----------------------------------
        #region Private methods
        private void BackupTravelers()
        {
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string contents = "";
            foreach (Traveler traveler in m_travelers)
            {
                contents += traveler.ToString();

            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(exeDir, "travelers.json"), contents);
        }
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
        private void ImportStoredTravelers()
        {
            //--------------------------------------------------------------
            // get the list of travelers and orders that have been created
            //--------------------------------------------------------------
            // create the file if it doesn't exist
            StreamWriter w = File.AppendText("travelers.json");
            w.Close();
            // open the file
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string line;
            System.IO.StreamReader file = new System.IO.StreamReader(System.IO.Path.Combine(exeDir, "travelers.json"));
            double travelerCount = File.ReadLines(System.IO.Path.Combine(exeDir, "travelers.json")).Count();
            int index = 0;
            while ((line = file.ReadLine()) != null && line != "")
            {
                Server.Write("\r{0}%", "Loading travelers from backup..." + Convert.ToInt32((Convert.ToDouble(index) / travelerCount) * 100));

                Dictionary<string, string> obj = (new StringStream(line)).ParseJSON();
                // check to see if these orders have been printed already
                // cull orders that do not exist anymore
                Traveler traveler = null;
                switch ((obj["type"])) {
                    case "Table": traveler = (Traveler)new Table(line); break;
                    case "Chair": traveler = (Traveler)new Chair(line); break;
                }
                if (traveler != null)
                {
                    m_travelers.Add(traveler);
                }
                index++;
                //if (traveler.ParentOrders.Count > 0)
                //{
                //    // import type-specific information
                //    switch (obj["type"])
                //    {
                //        case "Table":
                //            Table table = new Table(traveler,true);
                //            // Relational -------------------------------
                //            table.ParentOrders = traveler.ParentOrders;
                //            //-------------------------------------------
                //            table.ImportPart(ref m_MAS);
                //            if (table.Station == Traveler.GetStation("Start")) table.Start();
                //            table.Advance();
                //            m_tableManager.FinalizeTable(table);
                //            m_travelers.Add(table);
                //            break;
                //        case "Chair":
                //            Chair chair = new Chair(traveler,true);
                //            // Relational -------------------------------
                //            chair.ParentOrders = chair.ParentOrders;
                //            chair.Parents = traveler.Parents;
                //            chair.Children = traveler.Children;
                //            //-------------------------------------------
                //            chair.ImportPart(ref m_MAS);
                //            if (chair.Station == Traveler.GetStation("Start")) chair.Start();
                //            chair.Advance();
                //            m_travelers.Add(chair);
                //            break;
                //    }

                //}

            }
            Server.Write("\r{0}", "Loading travelers from backup...Finished\n");

            file.Close();
        }
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
        private void OnTravelersChanged()
        {
            // Update the travelers.json file with all the current travelers
            BackupTravelers();
            // fire the event
            TravelersChanged();
        }

       

        #endregion
        //----------------------------------
        #region Private member variables
        private List<Traveler> m_travelers;
        private IOrderManager m_orderManager;

        public event TravelersChangedSubscriber TravelersChanged;
        
        #endregion
    }
}
