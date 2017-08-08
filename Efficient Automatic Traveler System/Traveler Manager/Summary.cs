using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;
namespace Efficient_Automatic_Traveler_System
{
    enum SummarySort
    {
        Active,
        Available,
        Sorted,
        Finished, // travelers that have finished items
        Scrap,
        All,
        PreProcess
    }
    enum SummaryType
    {
        Production,
        PartialProduction,
        Scrap,
        Traveler,
        Rates
    }
    class Summary
    {
        #region Public methods
        public Summary()
        {
            m_travelers = new List<Traveler>();
            m_users = new List<User>();
        }
        public Summary(ITravelerManager travelerManager,string travelerType = "traveler", SummarySort sortType = SummarySort.Active) : this()
        {
            m_sort = sortType;
            m_travelerType = typeof(Traveler).Assembly.GetType("Efficient_Automatic_Traveler_System." + travelerType);
            switch (m_sort) {
                case SummarySort.Active: m_travelers = travelerManager.GetTravelers.Where(x => x.GetType() == m_travelerType && x.State == GlobalItemState.InProcess && x.Station != StationClass.GetStation("Start")).ToList(); break;
                case SummarySort.Available: m_travelers = travelerManager.GetTravelers.Where(x => x.GetType() == m_travelerType && x.State == GlobalItemState.PreProcess && x.Station == StationClass.GetStation("Start") && x.Quantity > 0).ToList(); break;
                case SummarySort.Sorted: m_travelers = travelerManager.GetTravelers.Where(x => x.GetType() == m_travelerType && x.State == GlobalItemState.PreProcess && x.Station != StationClass.GetStation("Start")).ToList(); break;
                case SummarySort.All: m_travelers = travelerManager.GetTravelers; break;
                case SummarySort.PreProcess: m_travelers = travelerManager.GetTravelers.Where(x => x.GetType() == m_travelerType && x.State == GlobalItemState.PreProcess && x.Quantity > 0).ToList(); break;

                default:
                    break;
            }
        }
        public Summary(ITravelerManager travelerManager,string travelerType = "Traveler", GlobalItemState? state = null, StationClass station = null)
        {
            m_travelerType = typeof(Traveler).Assembly.GetType("Efficient_Automatic_Traveler_System." + travelerType);
            m_travelers = travelerManager.GetTravelers.Where(x => x.GetType() == m_travelerType && (station == null || x.Station == station) && (!state.HasValue || x.State == state.Value)).ToList();
        }
        /* Creates a summary from two different system states, stored in two sets of files.
         Data is loaded into separate managers for each.
         A summary is then created from each state.
         These two summaries are then compared to yield the final summary which includes
         Delta totals between the time frames.
        */
        public Summary(DateTime A, DateTime B, string travelerType = "traveler", SummarySort sortType = SummarySort.Active) : this()
        {
            // starting state
            OrderManager orderManagerA = new OrderManager();
            orderManagerA.Import(A);
            TravelerManager travelerManagerA = new TravelerManager(orderManagerA as IOrderManager);
            travelerManagerA.Import(A);
            Summary summaryA = new Summary(travelerManagerA as ITravelerManager, travelerType, sortType);
            // ending state
            OrderManager orderManagerB = new OrderManager();
            orderManagerB.Import(B);
            TravelerManager travelerManagerB = new TravelerManager(orderManagerB as IOrderManager);
            travelerManagerB.Import(B);
            Summary summaryB = new Summary(travelerManagerB as ITravelerManager, travelerType, sortType);

            // Delta state (B's state - A's state)
            m_travelers = (summaryB - summaryA).Travelers;
        }
        public string ExportCSV(SummaryType type)
        {
            switch (type)
            {
                case SummaryType.PartialProduction: return PartialProductionCSV();
                case SummaryType.Production: return ProductionCSV();
                case SummaryType.Scrap: return ScrapCSV();
                case SummaryType.Traveler: return MakeCSV();
                case SummaryType.Rates: return RatesCSV();
                default: return "";
            }
        }
        public Summary(DateTime A, DateTime B) : this()
        {
            Begin = A;
            End = B;
            m_users = new List<User>();
            List<DateTime> datesDescending = new List<DateTime>(BackupManager.BackupDates);
            datesDescending.Sort((a, b) => b.CompareTo(a));
            int indexA = datesDescending.IndexOf(datesDescending.First(d => d <= B));
            int indexB = datesDescending.IndexOf(datesDescending.Last(d => d >= A));
            for (int index = indexA; index < indexB; index++)
            {
                try
                {
                    DateTime day = datesDescending[index];

                    OrderManager orderManager = new OrderManager();
                    orderManager.Import(day);
                    TravelerManager travelerManager = new TravelerManager(orderManager as IOrderManager);
                    travelerManager.Import(day);
                    // only add travelers with IDs that are not currently in the list
                    m_travelers.AddRange(travelerManager.GetTravelers.Where(t => !m_travelers.Exists(s => s.ID == t.ID)));

                    // Users

                    UserManager userManager = new UserManager();
                    userManager.Import(day);
                    m_users.AddRange(userManager.Users.Where(u => !m_users.Exists(v => v.UID == u.UID)));
                    foreach (User user in m_users)
                    {
                        user.History.AddRange(userManager.Users.Find(u => u.UID == user.UID).History);
                    }
                } catch (Exception ex)
                {
                    Server.WriteLine("Error retrieving history");
                    Server.LogException(ex);
                }
            }
        }
        public override string ToString()
        {
            List<string> items = new List<string>();
            foreach (Traveler traveler in m_travelers)
            {
                items.Add(traveler.ExportSummary());
            }
            Dictionary<string, string> obj = new Dictionary<string, string>() {
                {"sort", m_sort.ToString().Quotate() },
                {"items",items.Stringify(false) }
            };
            return obj.Stringify();
        }
        public static Summary operator -(Summary B, Summary A)
        {
            foreach (Traveler travelerB in B.Travelers)
            {
                // find the old version of this traveler
                Traveler travelerA = A.Travelers.Find(x => x.ID == travelerB.ID);
                if (travelerA != null)
                {
                    foreach (TravelerItem itemB in travelerB.Items)
                    {
                        // find the old version of this item
                        TravelerItem itemA = travelerA.Items.Find(x => x.ID == itemB.ID);
                        if (itemA != null)
                        {
                            // remove common history
                            itemB.History.RemoveAll(e => e == itemA.History.Find(eA => eA.Date == e.Date));
                        }
                    }
                }
            }
            return B;
        }
        public string UserCSV()
        {
            string webLocation = "./user summary.csv";
            DataTable summary = new DataTable();
            summary.Columns.Add(new DataColumn("Name"));
            summary.Columns.Add(new DataColumn("UID"));
            summary.Columns.Add(new DataColumn("Access Level"));
            summary.Columns.Add(new DataColumn("Qty Scrapped"));
            summary.Columns.Add(new DataColumn("Completed"));
            summary.Columns.Add(new DataColumn("Idle Time"));
            foreach (string stationName in StationClass.StationNames())
            {
                summary.Columns.Add(new DataColumn(stationName));
            }
            foreach (User user in m_users)
            {
                DataRow row = summary.NewRow();
                row["Name"] = user.Name;
                row["UID"] = user.UID;
                row["Access Level"] = user.AccessLevel;
                row["Qty Scrapped"] = m_travelers.Sum(t => t.Items.Count(i => i.History.OfType<ScrapEvent>().ToList().Exists(e => e.User.UID == user.UID)));
                row["Completed"] = m_travelers.Sum(t => t.Items.Sum(i => i.History.OfType<ProcessEvent>().ToList().Count(e => e.User.UID == user.UID)));
                // calculate total log time
                row["Idle Time"] = user.TotalLogTime();
                summary.Rows.Add(row);
            }
            File.WriteAllText(Path.Combine(Server.RootDir, "EATS Client", "user summary.csv"), summary.ToCSV());
            return webLocation;
        }
        public string MakeCSV()
        {
            string webLocation = "./summary.csv";
            List<string> contents = new List<string>();
            // add the header
            contents.Add((string)m_travelerType.GetMethod("ExportCSVheader").Invoke(null,null));
            // add each detail for each traveler
            foreach (Traveler traveler in m_travelers)
            {
                contents.Add(traveler.ExportCSVdetail());
            }

            File.WriteAllLines(Path.Combine(Server.RootDir, "EATS Client", "summary.csv"), contents.ToArray<string>());

            return webLocation;
        }
        public string InventorySummary()
        {
            string webLocation = "./inventory.csv";
            DataTable summary = new DataTable();
            summary.Columns.Add(new DataColumn("ItemCode"));
            foreach (StationClass station in StationClass.GetStations())
            {
                summary.Columns.Add(new DataColumn(station.Name));
            }
            foreach (string itemCode in Server.TravelerManager.GetTravelers.OfType<Table>().Select(t => t.ItemCode).Distinct())
            {
                DataRow row = summary.NewRow();
                row["ItemCode"] = itemCode;
                foreach (StationClass station in StationClass.GetStations())
                {

                    row[station.Name] = Server.TravelerManager.GetTravelers.Where(t => t.ItemCode == itemCode).Sum(t => t.Items.Count(i => i.Station == station));

                }
                summary.Rows.Add(row);
            }
            File.WriteAllText(Path.Combine(Server.RootDir, "EATS Client", "inventory.csv"), summary.ToCSV());

            return webLocation;
        }
        #endregion
        #region Private methods
        public string RatesCSV()
        {
            //--------------------------------------------
            string webLocation = "./rates.csv";
            DataTable summary = new DataTable();
            summary.Columns.Add(new DataColumn("Shape"));
            foreach (string station in StationClass.GetStations().Select(s => s.Name))
            {
                summary.Columns.Add(new DataColumn(station));
            }
            foreach (string shape in Server.TravelerManager.GetTravelers.OfType<Table>().Select(t => t.ShapeNo).Distinct())
            {
                DataRow row = summary.NewRow();
                row["Shape"] = shape;
                foreach (StationClass station in StationClass.GetStations())
                {
                    List<ProcessEvent> completions = Server.TravelerManager.GetTravelers.OfType<Table>().Where(
                        t => t.ShapeNo == shape
                    )
                    .SelectMany(
                        t => t.Items.SelectMany(
                            i => i.History.OfType<ProcessEvent>().Where(
                                e => e.Date >= Begin && e.Date <= End && e.Process == ProcessType.Completed && e.Station == station
                            )
                        )
                    ).ToList();
                    row[station.Name] = string.Join(" ", completions.Select(c => Math.Round(c.Duration,1)));
                }
                summary.Rows.Add(row);
            }
            File.WriteAllText(Path.Combine(Server.RootDir, "EATS Client", "rates.csv"), summary.ToCSV());
            return webLocation;
        }
        public string ProductionCSV()
        {
            //--------------------------------------------
            string webLocation = "./production.csv";
            DataTable summary = new DataTable();

            string stationType = "contourEdgebander";

            summary.Columns.Add(new DataColumn("ItemCode"));
            summary.Columns.Add(new DataColumn("Quantity"));
            summary.Columns.Add(new DataColumn("Date"));
            summary.Columns.Add(new DataColumn("Travelers"));
            summary.Columns.Add(new DataColumn("Items"));
            foreach (StationClass station in StationClass.GetStations().Where(s => s.Type == stationType))
            {
                summary.Columns.Add(new DataColumn(station.Name));
            }
            foreach (string itemCode in Server.TravelerManager.GetTravelers.OfType<Table>().Select(t => t.ItemCode).Distinct())
            {
                List<TravelerItem> items = Server.TravelerManager.GetTravelers.Where(t => t.ItemCode == itemCode).SelectMany(t => t.Items.Where(i => i.BeenCompletedDuring(DateTime.Today))).ToList();
                if (items.Any())
                {
                    DataRow row = summary.NewRow();
                    row["ItemCode"] = itemCode;

                    row["Quantity"] = items.Count;
                    row["Date"] = DateTime.Today.ToString("MM/dd/yyyy");
                    row["Travelers"] = items.Select(t => t.Parent.PrintID()).Distinct().ToList().Aggregate((i, j) => i + ' ' + j);
                    row["Items"] = items.GroupBy(i => i.Parent.ID).SelectMany(g => g.Select(i => i.SequenceNo.ToString())).Aggregate((i, j) => i + '|' + j);
                    foreach (StationClass station in StationClass.GetStations().Where(s => s.Type == stationType))
                    {
                        row[station.Name] = items.Count(i => i.History.OfType<ProcessEvent>().Any(e => e.Station.Type == stationType && e.Process == ProcessType.Completed)
                        && i.History.OfType<ProcessEvent>().Where(e => e.Station.Type == stationType && e.Process == ProcessType.Completed).Last().Station == station);
                    }
                    summary.Rows.Add(row);
                }
            }
            File.WriteAllText(Path.Combine(Server.RootDir, "EATS Client", "production.csv"), summary.ToCSV());

            return webLocation;
        }
        public string PartialProductionCSV()
        {
            //--------------------------------------------
            string webLocation = "./partial production.csv";
            DataTable summary = new DataTable();
            summary.Columns.Add(new DataColumn("ItemCode"));
            foreach (string station in StationClass.GetStations().Select(s => s.Name))
            {
                summary.Columns.Add(new DataColumn(station));
            }
            foreach (string itemCode in Server.TravelerManager.GetTravelers.OfType<Table>().Select(t => t.ItemCode).Distinct())
            {
                DataRow row = summary.NewRow();
                row["ItemCode"] = itemCode;
                foreach (StationClass station in StationClass.GetStations())
                {
                    int qty = Server.TravelerManager.GetTravelers.Where(t =>
                        t.ItemCode == itemCode).Sum(t => t.Items.Count(i => i.BeenCompletedAtDuring(station,DateTime.Today)));
                    if (qty > 0)
                    {
                        row[station.Name] = qty;
                    }
                }
                summary.Rows.Add(row);
            }
            File.WriteAllText(Path.Combine(Server.RootDir, "EATS Client", "partial production.csv"), summary.ToCSV());

            return webLocation;
        }
        public static string HumanizeDictionary<Tkey,TValue>(Dictionary<Tkey,TValue> dictionary)
        {
            string result = "";
            string separator = " : ";
            int keyColWidth = dictionary.Max(pair => pair.Key.ToString().Length);
            int valueColWidth = dictionary.Max(pair => pair.Value.ToString().Length);

            foreach (KeyValuePair<Tkey,TValue> pair in dictionary) {
                result += pair.Key.ToString() + separator + pair.Value.ToString() + Environment.NewLine;
                result += "".PadLeft(keyColWidth + separator.Length + valueColWidth, '_') + Environment.NewLine;
            }
            return result;
        }
        public static Dictionary<string, string> ScrapDetail(Traveler traveler, TravelerItem scrap)
        {
            ScrapEvent scrapEvent = scrap.History.OfType<ScrapEvent>().FirstOrDefault();

            Dictionary<string, string> detail = new Dictionary<string, string>();
            detail.Add("Item", scrap.PrintID());
            detail.Add("Part", traveler.ItemCode);

            detail.Add("User", scrapEvent.User.Name);
            detail.Add("Station", scrapEvent.Station.Name);
            detail.Add("Date", scrapEvent.Date.ToString("MM/dd/yyyy @ hh:mm tt"));
            detail.Add("Time", Math.Round(scrapEvent.Duration, 2).ToString());
            detail.Add("Started Work", scrapEvent.StartedWork.ToString());
            detail.Add("Source", scrapEvent.Source);
            detail.Add("Reason", scrapEvent.Reason);
            return detail;
        }
        public string ScrapCSV()
        {
            DateTime date = DateTime.Today;
            string webLocation = "./scrap.csv";
            List<string> contents = new List<string>();
            // add the header
            //contents.Add(new List<string>() { "Part", "Quantity", "Date" }.Stringify<string>());
            // add each detail for each traveler
            List<string> fields = new List<string>() {"Item","Part", "User","Station","Date","Time","Started Work","Source","Reason" };

            DataTable summary = new DataTable();
            summary.TableName = "Enter production for these items prior to scrapping them";
            summary.Columns.Add(new DataColumn("Item ID"));
            summary.Columns.Add(new DataColumn("ItemCode"));
            summary.Columns.Add(new DataColumn("User"));
            summary.Columns.Add(new DataColumn("Station"));
            summary.Columns.Add(new DataColumn("Date"));
            summary.Columns.Add(new DataColumn("Started Work"));
            summary.Columns.Add(new DataColumn("Source"));
            summary.Columns.Add(new DataColumn("Reason"));

            foreach (TravelerItem item in Server.TravelerManager.GetTravelers.SelectMany(t => t.Items.Where(i => i.Scrapped)))
            {
                ScrapEvent scrapEvent;
                if (item.GetScrapEvent(out scrapEvent) && scrapEvent.Date.Day == date.Day)
                {
                    DataRow row = summary.NewRow();
                    row["Item ID"] = item.PrintID();
                    row["ItemCode"] = item.ItemCode;
                    row["User"] = scrapEvent.User.Name;
                    row["Station"] = scrapEvent.Station.Name;
                    row["Date"] = scrapEvent.Date.ToString("MM/dd/yyyy");
                    row["Started Work"] = scrapEvent.StartedWork.Print();
                    row["Source"] = scrapEvent.Source;
                    row["Reason"] = scrapEvent.Reason;
                    summary.Rows.Add(row);
                }
                
            }

            File.WriteAllText(Path.Combine(Server.RootDir, "EATS Client", "scrap.csv"), summary.ToCSV());

            return webLocation;
            //List<Dictionary<string, string>> scrapped = new List<Dictionary<string, string>>();
            //foreach (Traveler traveler in m_travelers)
            //{
            //    foreach (TravelerItem scrap in traveler.Items)
            //    {
            //        if (scrap.Scrapped)
            //        {
            //            ScrapEvent scrapEvent = scrap.History.OfType<ScrapEvent>().ToList().FirstOrDefault();
            //            if (scrapEvent != null && scrapEvent.Date >= DateTime.Today)
            //            {
            //                scrapped.Add(Summary.ScrapDetail(traveler,scrap));
            //            }
            //        }
            //    }
            //}
            //// add the header
            //contents.Add(fields.Stringify<string>().Trim('[').Trim(']'));

            //foreach (Dictionary<string, string> detail in scrapped)
            //{
            //    List<string> row = new List<string>();
            //    foreach (string field in fields)
            //    {
            //        if (detail.ContainsKey(field))
            //        {
            //            row.Add(detail[field]);
            //        }
            //        else
            //        {
            //            row.Add("");
            //        }
            //    }
            //    contents.Add(row.Stringify<string>().Trim('[').Trim(']'));
            //}

        }
        public string ReworkCSV()
        {
            string webLocation = "./rework.csv";
            List<string> contents = new List<string>();
            // add the header
            //contents.Add(new List<string>() { "Part", "Quantity", "Date" }.Stringify<string>());
            // add each detail for each traveler
            List<string> fields = new List<string>() {"Traveler", "Part", "Quantity"};
            List<Dictionary<string, string>> rework = new List<Dictionary<string, string>>();
            foreach (Traveler traveler in m_travelers.Where(t => t.State == GlobalItemState.InProcess))
            {
                int unaccountedScrap = traveler.Items.Count(i => i.Scrapped && i.ID > traveler.LastReworkAccountedFor);

                int qtyPending = traveler.QuantityPendingAt(traveler.Station);


                int quantity = Math.Min(qtyPending,unaccountedScrap);
                if (quantity > 0)
                {
                    Dictionary<string, string> item = new Dictionary<string, string>();
                    item.Add("Traveler", traveler.PrintID());
                    item.Add("Part", traveler.ItemCode);
                    item.Add("Quantity", quantity.ToString());
                    rework.Add(item);
                    traveler.LastReworkAccountedFor = traveler.Items.Max(i => i.ID);
                }
            }
            // add the header
            contents.Add(fields.Stringify<string>().Trim('[').Trim(']'));

            foreach (Dictionary<string, string> detail in rework)
            {
                List<string> row = new List<string>();
                foreach (string field in fields)
                {
                    if (detail.ContainsKey(field))
                    {
                        row.Add(detail[field]);
                    }
                    else
                    {
                        row.Add("");
                    }
                }
                contents.Add(row.Stringify<string>().Trim('[').Trim(']'));
            }

            File.WriteAllLines(Path.Combine(Server.RootDir, "EATS Client", "rework.csv"), contents.ToArray<string>());

            return webLocation;
        }
        public string CSV(string path, List<SummaryColumn> columns)
        {
            List<string> rows = new List<string>();
            // header
            List<string> header = new List<string>();
            foreach (SummaryColumn column in columns)
            {
                header.Add(column.Header);
            }
            rows.Add(header.Stringify().Trim('[',']'));
            // detail
            foreach (object obj in m_travelers)
            {
                List<string> row = new List<string>();
                foreach (SummaryColumn column in columns)
                {
                    row.Add(column.GetProperty(obj));
                }
                rows.Add(row.Stringify().Trim('[', ']'));
            }
            File.WriteAllLines(Path.Combine(Server.RootDir, path), rows.ToArray<string>());
            return "../" + path;
        }
        #endregion
        #region Properties
        private SummarySort m_sort;
        private Type m_travelerType;
        private List<Traveler> m_travelers;
        private List<User> m_users;
        public DateTime Begin { get; set; }
        public DateTime End { get; set; }
        public string FileSuffix
        {
            get
            {
                return " " + Begin.ToString("MM-dd-yy") + " to " + End.ToString("MM-dd-yy");
            }
        }
        #endregion
        #region Interface
        public List<Traveler> Travelers
        {
            get
            {
                return m_travelers;
            }
        }

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
