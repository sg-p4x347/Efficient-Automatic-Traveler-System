﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data.Odbc;
using System.Net;
using System.Net.Http;

namespace Efficient_Automatic_Traveler_System
{
    enum TravelerEvent
    {
        Completed,
        Scrapped,
        Reworked,
        Moved,
        Merged
    }
    class Event
    {
        public Event (string json)
        {
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                type = (TravelerEvent)Enum.Parse(typeof(TravelerEvent), obj["type"]);
                date = obj["date"];
                time = Convert.ToDouble(obj["time"]);
                station = Convert.ToInt32(obj["station"]);
            }
            catch (Exception ex)
            {
                Server.WriteLine("Problem when reading event from file: " + ex.Message + "; StackTrace: " + ex.StackTrace);
            }
        }
        public Event (TravelerEvent e, double t, int s)
        {
            type = e;
            time = 0;
            station = s;
            date = DateTime.Now.ToString("MM/dd/yy @ hh:00");
        }
        public override string ToString()
        {
            string json = "";
            json += "{";
            json += "\"type\":" + type.ToString("d") + ",";
            json += "\"date\":" + '"' + date + '"' + ",";
            json += "\"time\":" + time + ",";
            json += "\"station\":" + station;
            json += "}";
            return json;
        }
        public TravelerEvent type;
        public double time;
        public int station;
        public string date;
    }
    struct NameValueQty<valueType,qtyType>
    {
        public NameValueQty(string name, valueType value, qtyType qty)
        {
            Name = name;
            Value = value;
            Qty = qty;
        }
        public override string ToString()
        {
            string json = "";
            json += '{';
            json += "\"name\":" + '"' + Name.Replace("\"","\\\"") + '"' + ',';
            json += "\"value\":" + '"' + Value.ToString().Replace("\"", "\\\"") + '"' + ',';
            json += "\"qty\":" + '"' + Qty.ToString().Replace("\"", "\\\"") + '"';
            json += '}';
            return json;
        }
        public string Name;
        public valueType Value;
        public qtyType Qty;
    }
    class TravelerItem
    {
        public TravelerItem(UInt16 ID)
        {
            m_ID = ID;
            m_scrapped = false;
            m_station = -1;
            m_lastStation = -1;
            m_history = new List<Event>();
        }
        public TravelerItem(string json)
        {
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                m_ID = Convert.ToUInt16(obj["ID"]);
                m_scrapped = Convert.ToBoolean(obj["scrapped"]);
                m_station = Convert.ToInt32(obj["station"]);
                m_lastStation = Convert.ToInt32(obj["lastStaion"]);
                m_history = new List<Event>();
                foreach (string eventString in (new StringStream(obj["history"])).ParseJSONarray())
                {
                    m_history.Add(new Event(eventString));
                }
            }
            catch (Exception ex)
            {
                Server.WriteLine("Problem when reading TravelerItem from file: " + ex.Message + "; StackTrace: " + ex.StackTrace);
            }
        }
        public override string ToString()
        {
            string json = "{";
            json += "\"ID\":" + m_ID;
            json += ",\"scrapped\":" + '"' + m_scrapped + '"';
            json += ",\"station\":" + m_station;
            json += ",\"lastStation\":" + m_lastStation;
            json += ",\"history\":" + m_history.Stringify<Event>();
            json += '}';
            return json;
        }
        // Properties
        private UInt16 m_ID;
        private bool m_scrapped;
        private int m_station;
        private int m_lastStation;
        private List<Event> m_history;

        public ushort ID
        {
            get
            {
                return m_ID;
            }
        }

        public int Station
        {
            get
            {
                return m_station;
            }

            set
            {
                m_lastStation = m_station;
                m_station = value;
            }
        }

        public int LastStation
        {
            get
            {
                return m_lastStation;
            }

            set
            {
                m_lastStation = value;
            }
        }

        public bool Scrapped
        {
            get
            {
                return m_scrapped;
            }

            set
            {
                m_scrapped = value;
            }
        }

        internal List<Event> History
        {
            get
            {
                return m_history;
            }

            set
            {
                m_history = value;
            }
        }
        public bool IsComplete()
        {
            foreach (Event evt in History)
            {
                if (evt.station == Station && evt.type == TravelerEvent.Completed)
                {
                    return true;
                }
            }
            return false;
        }
    }
    abstract class Traveler
    {
        #region Public Methods
        public Traveler() { }
        // Gets the base properties and orders of the traveler from a json string
        public Traveler(string json)
        {
            Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
            m_ID = Convert.ToInt32(obj["ID"]);
            
            m_quantity = Convert.ToInt32(obj["quantity"]);
            m_part = new Bill(obj["itemCode"], m_quantity);
            Items = new List<TravelerItem>();
            foreach (string item in (new StringStream(obj["items"])).ParseJSONarray())
            {
                Items.Add(new TravelerItem(item));
            }
            m_parentOrders = (new StringStream(obj["parentOrders"])).ParseJSONarray();
            m_station = Traveler.GetStation(obj["station"]);
        }
        // Creates a traveler from a part number and quantity, then loads the bill of materials
        //public Traveler(string billNo, int quantity, ref OdbcConnection MAS)
        //{
        //    // set META information
        //    m_part = new Bill(billNo, quantity, ref MAS);
        //    m_quantity = quantity;
        //    NewID();

        //    // Import the part
        //    ImportPart(ref MAS);
        //}
        public Traveler(string billNo, int quantity)
        {
            // set META information
            m_part = new Bill(billNo, quantity);
            m_quantity = quantity;
            m_parentOrders = new List<string>();
            Items = new List<TravelerItem>();
            NewID();
        }
        public virtual void ImportPart(IOrderManager orderManager, ref OdbcConnection MAS)
        {
            m_part = new Bill(m_part.BillNo, m_quantity, ref MAS);
        }
        public void NewID()
        {
            // open the currentID.txt file
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            System.IO.StreamReader readID = new StreamReader(System.IO.Path.Combine(exeDir, "currentID.txt"));
            m_ID = Convert.ToInt32(readID.ReadLine());
            readID.Close();
            // increment the current ID
            File.WriteAllText(System.IO.Path.Combine(exeDir, "currentID.txt"), (m_ID + 1).ToString() + '\n');
        }
        // Finds all the components in the top level bill, setting key components along the way
        //public void FindComponents(Bill bill)
        //{
        //    // find work and or material
        //    foreach (Item componentItem in bill.ComponentItems)
        //    {
        //        // update the component's total quantity
        //        componentItem.TotalQuantity = bill.TotalQuantity * componentItem.QuantityPerBill;
        //        // sort out key components
        //        string itemCode = componentItem.ItemCode;
        //        if (itemCode == "/LWKE1" || itemCode == "/LWKE2" || itemCode == "/LCNC1" || itemCode == "/LCNC2")
        //        {
        //            // CNC labor
        //            if (m_cnc == null)
        //            {
        //                m_cnc = componentItem;
        //            } else
        //            {
        //                m_cnc.TotalQuantity += componentItem.TotalQuantity;
        //            }
        //        }
        //        else if (itemCode == "/LBND2" || itemCode == "/LBND3")
        //        {
        //            // Straight Edgebander labor
        //            if (m_ebander == null)
        //            {
        //                m_ebander = componentItem;
        //            } else
        //            {
        //                m_ebander.TotalQuantity += componentItem.TotalQuantity;
        //            }
        //        }
        //        else if (itemCode == "/LPNL1" || itemCode == "/LPNL2")
        //        {
        //            // Panel Saw labor
        //            if (m_saw == null)
        //            {
        //                m_saw = componentItem;
        //            } else
        //            {
        //                m_saw.TotalQuantity += componentItem.TotalQuantity;
        //            }
        //        }
        //        else if (itemCode == "/LCEB1" | itemCode == "/LCEB2")
        //        {
        //            // Contour Edge Bander labor (vector)
        //            if (m_vector == null)
        //            {
        //                m_vector = componentItem;
        //            } else
        //            {
        //                m_vector.TotalQuantity += componentItem.TotalQuantity;
        //            }
        //        }
        //        else if ( itemCode == "/LATB1" || itemCode == "/LATB2" || itemCode == "/LATB3" || itemCode == "/LACH1" || itemCode == "/LACH2" || itemCode == "/LACH3")
        //        {
        //            // Assembly labor
        //            if (m_assm == null)
        //            {
        //                m_assm = componentItem;
        //            } else
        //            {
        //                m_assm.TotalQuantity += componentItem.TotalQuantity;
        //            }
        //        }
        //        else if (itemCode == "/LBOX1")
        //        {
        //            // Box construction labor
        //            if (m_box == null)
        //            {
        //                m_box = componentItem;
        //            } else
        //            {
        //                m_box.TotalQuantity += componentItem.TotalQuantity;
        //            }
        //        }
        //        else if (itemCode.Substring(0, 3) == "006")
        //        {
        //            // Material
        //            if (m_material == null)
        //            {
        //                m_material = componentItem;
        //            } else
        //            {
        //                m_material.TotalQuantity += componentItem.TotalQuantity;
        //            }
        //        }
        //        else if (itemCode.Substring(0, 2) == "87")
        //        {
        //            // Edgeband
        //            if (m_eband == null)
        //            {
        //                m_eband = componentItem;
        //            } else
        //            {
        //                m_eband.TotalQuantity += componentItem.TotalQuantity;
        //            }
        //        }
        //        else if (m_box == null && itemCode.Substring(0, 2) == "90")
        //        {
        //            // Paid for box
        //            m_boxItemCode = itemCode;
        //        }
        //        else
        //        {
        //            // anything else
        //            // check the blacklist
        //            bool blacklisted = false;
        //            foreach (BlacklistItem blItem in m_blacklist )
        //            {
        //                if (blItem.StartsWith(itemCode))
        //                {
        //                    blacklisted = true;
        //                    break;
        //                }
        //            }
        //            if (!blacklisted)
        //            {
        //                // check for existing item first
        //                bool foundItem = false;
        //                foreach (Item component in m_components)
        //                {
        //                    if (component.ItemCode == itemCode)
        //                    {
        //                        foundItem = true;
        //                        component.TotalQuantity += componentItem.TotalQuantity;
        //                        break;
        //                    }
        //                }
        //                if (!foundItem)
        //                {
        //                    m_components.Add(componentItem);
        //                }
        //            }
        //        }
        //    }
        //    // Go deeper into each component bill
        //    foreach (Bill componentBill in bill.ComponentBills)
        //    {
        //        componentBill.TotalQuantity = bill.TotalQuantity * componentBill.QuantityPerBill;
        //        FindComponents(componentBill);
        //    }
        //}
        //check inventory to see how many actually need to be produced.
        //public void CheckInventory(ref OdbcConnection MAS)
        //{
        //    try
        //    {
        //        if (MAS.State != System.Data.ConnectionState.Open) throw new Exception("MAS is in a closed state!");
        //        OdbcCommand command = MAS.CreateCommand();
        //        command.CommandText = "SELECT QuantityOnSalesOrder, QuantityOnHand FROM IM_ItemWarehouse WHERE ItemCode = '" + m_part.BillNo + "'";
        //        OdbcDataReader reader = command.ExecuteReader();
        //        if (reader.Read())
        //        {
        //            int available = Convert.ToInt32(reader.GetValue(1)) - Convert.ToInt32(reader.GetValue(0));
        //            if (available >= 0)
        //            {
        //                // No parts need to be produced
        //                m_quantity = 0;
        //            }
        //            else
        //            {
        //                // adjust the quantity that needs to be produced
        //                m_quantity = Math.Min(-available, m_quantity);
        //            }
        //        }
        //        reader.Close();
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine("An error occured when accessing inventory: " + ex.Message);
        //    }
        //}
        //public void Import(string json)
        //{
        //    try
        //    {
        //        bool readString = false;
        //        string stringToken = "";

        //        string memberName = "";

        //        string value = "";

        //        // SalesOrderNo
        //        for (int pos = 0; pos < json.Length; pos++)
        //        {
        //            char ch = json[pos];
        //            switch (ch)
        //            {
        //                case ' ':
        //                case '\t':
        //                case '\n':
        //                    continue;
        //                case '"':
        //                    readString = !readString;
        //                    continue;
        //                case ':':
        //                    memberName = stringToken; stringToken = "";
        //                    continue;
        //                case '[':
        //                    while (json[pos] != ']')
        //                    {
        //                        if (json[pos] == '{')
        //                        {
        //                            string orderJson = "";
        //                            while (json[pos] != '}')
        //                            {
        //                                ch = json[pos];
        //                                orderJson += ch;
        //                                pos++;
        //                            }
        //                            m_orders.Add(new Order(orderJson + '}'));
        //                        }
        //                        pos++;
        //                    }
        //                    continue;
        //                case ',':
        //                    value = stringToken; stringToken = "";
        //                    // set the corresponding member
        //                    if (memberName == "ID")
        //                    {
        //                        m_ID = Convert.ToInt32(value);
        //                    }
        //                    else if (memberName == "itemCode")
        //                    {
        //                        m_partNo = value;
        //                    }
        //                    else if (memberName == "quantity")
        //                    {
        //                        m_quantity = Convert.ToInt32(value);
        //                    }
        //                    else if (memberName == "station")
        //                    {
        //                        m_station = Convert.ToInt32(value);
        //                    }
        //                    continue;
        //                case '}': continue;
        //            }
        //            if (readString)
        //            {
        //                // read string character by character
        //                stringToken += ch;
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine("Problem reading in traveler from printed.json: " + ex.Message);
        //    }
        //    m_printed = true;
        //}
        // returns a JSON formatted string containing traveler information
        public override string ToString()
        {
            string json = "";
            json += "{";
            // BASIC PROPERTIES
            json += "\"ID\":" + m_ID + ",";
            json += "\"itemCode\":" + '"' + m_part.BillNo + '"' + ",";
            json += "\"quantity\":" + m_quantity + ",";
            // ITEMS [...]
            json += "\"items\":" + Items.Stringify<TravelerItem>() + ',';
            // PARENT ORDERS [...]
            json += "\"parentOrders\":" + m_parentOrders.Stringify<string>() + ',';
            // UNIFIED STATION
            json += "\"station\":" + '"' + Traveler.GetStationName(Station) + '"';
            // packs in members specific to derived classes
            json += ExportProperties(); 

            json += "}\n";
            return json;
        }
        // print a label for this traveler
        public void PrintLabel(int qty = 1)
        {
            try
            {
                string result = "";
                using (var client = new WebClient())
                {
                    //client.Credentials = new NetworkCredential("gage", "Stargatep4x347");
                    client.Headers[HttpRequestHeader.ContentType] = "application/json";
                    string json = "{\"ID\":\"" + ID + "\",";
                    json += "\"Desc1\":\"" + Part.BillDesc + "\",";
                    json += "\"Desc2\":\"" + "Blank" + "\",";
                    //json += "\"Date\":\"" + DateTime.Today.ToString(@"yyyy\-MM\-dd") + "\",";
                    json += "\"template\":\"" + "4x2 Table Travel1" + "\",";
                    json += "\"qty\":" + qty + ",";
                    json += "\"printer\":\"" + "4x2Pack" + "\"}";

                    //result = client.UploadString(@"http://192.168.2.6:8080/printLabel", "POST", json);
                    //http://192.168.2.6:8080/printLabel
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Traveler [" + ID + "] could not print a label: " + ex.Message);
            }
        }
        
        public static bool IsTable(string s)
        {
            return s != null && ((s.Length == 9 && s.Substring(0, 2) == "MG") || (s.Length == 10 && (s.Substring(0, 3) == "38-" || s.Substring(0, 3) == "41-")));
        }
        public static bool IsChair(string s)
        {
            if (s.Length == 14 && s.Substring(0, 2) == "38")
            {
                string[] parts = s.Split('-');
                return (parts[0].Length == 5 && parts[1].Length == 4 && parts[2].Length == 3);
            }
            else if (s.Length == 15 && s.Substring(0, 4) == "MG11")
            {
                string[] parts = s.Split('-');
                return (parts[0].Length == 6 && parts[1].Length == 4 && parts[2].Length == 3);
            }
            else
            {
                return false;
            }

        }
        public static int GetStation(string key)
        {
            try
            {
                return Stations[key];
            } catch (Exception ex)
            {
                return -1;
            }
        }
        public static string GetStationName(int value)
        {
            try
            {
                foreach (KeyValuePair<string,int> pair in Stations)
                {
                    if (pair.Value == value)
                    {
                        return pair.Key;
                    }
                }
                return "None";
            }
            catch (Exception ex)
            {
                return "None";
            }
        }
        // Manually sets the station 
        //public virtual void MoveTo(int station)
        //{
        //    m_station = station;
        //}
        public TravelerItem FindItem(ushort ID)
        {
            return Items.Find(x => x.ID == ID);
        }
        public void ScrapItem(ushort ID)
        {
            FindItem(ID).Scrapped = true;
        }
        
        // advances all completed items at the specified station
        public void Advance(int station)
        {
            foreach (TravelerItem item in Items)
            {
                if (item.Station == station)
                {
                    AdvanceItem(item.ID);
                }
            }
        }


        #endregion
        //--------------------------------------------------------
        #region Abstract Methods
        // export for clients to display
        public abstract string Export(string clientType, int station);
        // advances the item to the next station
        public abstract void AdvanceItem(ushort ID);
        #endregion
        //--------------------------------------------------------
        #region Private Methods
        // overridden in derived classes, packs properties into the Export() json string
        protected abstract string ExportProperties();
        #endregion
        //--------------------------------------------------------
        #region Properties

        // general
        protected int m_ID;
        protected Bill m_part;
        protected int m_quantity;
        private List<TravelerItem> items;
        protected List<string> m_parentOrders;
        private int m_station;

        #endregion
        //--------------------------------------------------------
        #region Interface
        internal int ID
        {
            get
            {
                return m_ID;
            }
        }
        internal Bill Part
        {
            get
            {
                return m_part;
            }
        }

        internal int Quantity
        {
            get
            {
                return m_quantity;
            }

            set
            {
                m_quantity = value;
                //m_part.TotalQuantity = m_quantity;
                //FindComponents(m_part);
            }
        }
        internal string ItemCode
        {
            get
            {
                return m_part.BillNo;
            }
        }
        internal int Station
        {
            get
            {
                foreach (TravelerItem item in Items)
                {
                    m_station = item.Station;
                    if (item.Station != Items[0].Station)
                    {
                        m_station = -1;
                        return -1;
                    }
                }
                return m_station;
            }
            set
            {
                m_station = value;
            }
        }
        //internal string TimeStamp
        //{
        //    get
        //    {
        //        return m_timeStamp;
        //    }
        //    set
        //    {
        //        m_timeStamp = value;
        //    }
        //}

        //internal bool Printed
        //{
        //    get
        //    {
        //        return m_printed;
        //    }

        //    set
        //    {
        //        m_printed = value;
        //    }
        //}



        //internal string DrawingNo
        //{
        //    get
        //    {
        //        return m_drawingNo;
        //    }

        //    set
        //    {
        //        m_drawingNo = value;
        //    }
        //}



        //internal string Color
        //{
        //    get
        //    {
        //        return m_color;
        //    }

        //    set
        //    {
        //        m_color = value;
        //    }
        //}

        //internal Item Cnc
        //{
        //    get
        //    {
        //        return m_cnc;
        //    }

        //    set
        //    {
        //        m_cnc = value;
        //    }
        //}

        //internal Item Vector
        //{
        //    get
        //    {
        //        return m_vector;
        //    }

        //    set
        //    {
        //        m_vector = value;
        //    }
        //}

        //internal Item Ebander
        //{
        //    get
        //    {
        //        return m_ebander;
        //    }

        //    set
        //    {
        //        m_ebander = value;
        //    }
        //}

        //internal Item Saw
        //{
        //    get
        //    {
        //        return m_saw;
        //    }

        //    set
        //    {
        //        m_saw = value;
        //    }
        //}

        //internal Item Assm
        //{
        //    get
        //    {
        //        return m_assm;
        //    }

        //    set
        //    {
        //        m_assm = value;
        //    }
        //}

        //internal Item Box
        //{
        //    get
        //    {
        //        return m_box;
        //    }

        //    set
        //    {
        //        m_box = value;
        //    }
        //}

        //internal Item Material
        //{
        //    get
        //    {
        //        return m_material;
        //    }

        //    set
        //    {
        //        m_material = value;
        //    }
        //}

        //internal Item Eband
        //{
        //    get
        //    {
        //        return m_eband;
        //    }

        //    set
        //    {
        //        m_eband = value;
        //    }
        //}

        //internal List<Item> Components
        //{
        //    get
        //    {
        //        return m_components;
        //    }

        //    set
        //    {
        //        m_components = value;
        //    }
        //}

        //internal List<BlacklistItem> Blacklist
        //{
        //    get
        //    {
        //        return m_blacklist;
        //    }

        //    set
        //    {
        //        m_blacklist = value;
        //    }
        //}
        //internal int PartsPerBox
        //{
        //    get
        //    {
        //        return m_partsPerBox;
        //    }

        //    set
        //    {
        //        m_partsPerBox = value;
        //    }
        //}
        //internal string BoxItemCode
        //{
        //    get
        //    {
        //        return m_boxItemCode;
        //    }

        //    set
        //    {
        //        m_boxItemCode = value;
        //    }
        //}

        //internal string RegPack
        //{
        //    get
        //    {
        //        return m_regPack;
        //    }

        //    set
        //    {
        //        m_regPack = value;
        //    }
        //}

        //internal int RegPackQty
        //{
        //    get
        //    {
        //        return m_regPackQty;
        //    }

        //    set
        //    {
        //        m_regPackQty = value;
        //    }
        //}

        //internal string SupPack
        //{
        //    get
        //    {
        //        return m_supPack;
        //    }

        //    set
        //    {
        //        m_supPack = value;
        //    }
        //}

        //internal int SupPackQty
        //{
        //    get
        //    {
        //        return m_supPackQty;
        //    }

        //    set
        //    {
        //        m_supPackQty = value;
        //    }
        //}

        //internal int Station
        //{
        //    get
        //    {
        //        return m_station;
        //    }

        //    set
        //    {
        //        m_lastStation = m_station;
        //        m_station = value;
        //    }
        //}
        //internal int NextStation
        //{
        //    get
        //    {
        //        return m_nextStation;
        //    }

        //    set
        //    {
        //        m_nextStation = value;
        //    }
        //}

        //internal List<Event> History
        //{
        //    get
        //    {
        //        return m_history;
        //    }

        //    set
        //    {
        //        m_history = value;
        //    }
        //}

        internal List<string> ParentOrders
        {
            get
            {
                return m_parentOrders;
            }

            set
            {
                m_parentOrders = value;
            }
        }

        public List<TravelerItem> Items
        {
            get
            {
                return items;
            }

            set
            {
                items = value;
            }
        }
        #endregion
        //--------------------------------------------------------
        // static
        internal static Dictionary<string, int> Stations = new Dictionary<string, int>();
    }
}