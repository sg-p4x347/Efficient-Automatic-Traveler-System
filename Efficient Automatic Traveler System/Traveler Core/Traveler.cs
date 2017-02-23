using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data.Odbc;
using Excel = Microsoft.Office.Interop.Excel;
using Marshal = System.Runtime.InteropServices.Marshal;

namespace Efficient_Automatic_Traveler_System
{
    enum TravelerEvent
    {
        Completed
    }
    class Event
    {
        public Event (TravelerEvent e, DateTime t, int q, int s)
        {
            type = e;
            date = t;
            time = 0;
            quantity = q;
            station = s;
        }
        public Event(TravelerEvent e, int q, int s)
        {
            type = e;
            date = DateTime.Now;
            time = 0;
            quantity = q;
            station = s;
        }
        public Event(TravelerEvent e, int q, int s, double el)
        {
            type = e;
            date = DateTime.Now;
            time = el;
            quantity = q;
            station = s;
        }
        public string Export()
        {
            string json = "";
            json += "{";
            json += "\"type\":" + '"' + type.ToString() + '"' + ",";
            json += "\"date\":" + '"' + date.ToString("MM/dd/yyyy") + '"' + ",";
            json += "\"time\":" + '"' + time.ToString() + '"' + ",";
            json += "\"quantity\":" + '"' + quantity.ToString() + '"' + ",";
            json += "\"station\":" + '"' + station.ToString() + '"';
            json += "}";
            return json;
        }
        public TravelerEvent type;
        public DateTime date;
        public double time;
        public int quantity;
        public int station;
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
    class Traveler
    {
        //-----------------------
        // Public members
        //-----------------------

        // Doesn't do anything
        public Traveler()
        {

        }
        public Traveler(Dictionary<string,string> obj)
        {
            m_ID = Convert.ToInt32(obj["ID"]);
            m_partNo = obj["itemCode"];
            m_quantity = Convert.ToInt32(obj["quantity"]);
            m_station = Convert.ToInt32(obj["station"]);
            m_children = new List<int>();
            foreach (string child in (new StringStream(obj["children"])).ParseJSONarray())
            {
                m_children.Add(Convert.ToInt32(child));
            }
            m_parents = new List<int>();
            foreach (string parent in (new StringStream(obj["parents"])).ParseJSONarray())
            {
                m_parents.Add(Convert.ToInt32(parent));
            }
            m_parentOrders = (new StringStream(obj["parentOrders"])).ParseJSONarray();
        }
        // Copy constructor
        public Traveler(Traveler t)
        {
            // general
            m_part = t.Part;
            NewID(); // Every traveler must have a unique ID
            m_timeStamp = t.TimeStamp;
            m_printed = t.Printed;
            m_partNo = t.PartNo;
            m_drawingNo = t.DrawingNo;
            m_quantity = t.Quantity;
            m_color = t.Color;
            m_station = t.Station;
            m_nextStation = t.NextStation;
            m_history = t.History;
            // relational
            m_children = t.Children;
            m_parents = t.Parents;
            m_parentOrders = t.ParentOrders;
            // Labor
            m_cnc = t.Cnc;
            m_vector = t.Vector;
            m_ebander = t.Ebander;
            m_saw = t.Saw;
            m_assm = t.Assm;
            m_box = t.Box;
            // Material
            m_material = t.Material;
            m_eband = t.Eband;
            m_components = t.Components;
            m_blacklist = t.Blacklist;
            // Box
            m_partsPerBox = t.PartsPerBox;
            m_boxItemCode = t.BoxItemCode;
            m_regPack = t.RegPack;
            m_regPackQty = t.RegPackQty;
            m_supPack = t.SupPack;
            m_supPackQty = t.SupPackQty;
        }
        // Gets the base properties and orders of the traveler from a json string
        public Traveler(string json)
        {
            //Import(json);
        }
        // Creates a traveler from a part number and quantity
        public Traveler(string partNo, int quantity)
        {
            // set META information
            m_partNo = partNo;
            m_quantity = quantity;
            NewID();
        }
        // Creates a traveler from a part number and quantity, then loads the bill of materials
        public Traveler(string partNo, int quantity, OdbcConnection MAS)
        {
            // set META information
            m_partNo = partNo;
            m_quantity = quantity;
            NewID();

            // Import the part
            ImportPart(MAS);
        }
        public void ImportPart(OdbcConnection MAS)
        {
            if (m_partNo != "")
            {
                m_part = new Bill(m_partNo, m_quantity, MAS);
                m_drawingNo = m_part.DrawingNo;
                m_part.BillDesc = m_part.BillDesc.Replace("TableTopAsm,", ""); // tabletopasm is pretty obvious and therefore extraneous
                FindComponents(m_part);
            }
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
        public void FindComponents(Bill bill)
        {
            // find work and or material
            foreach (Item componentItem in bill.ComponentItems)
            {
                // update the component's total quantity
                componentItem.TotalQuantity = bill.TotalQuantity * componentItem.QuantityPerBill;
                // sort out key components
                string itemCode = componentItem.ItemCode;
                if (itemCode == "/LWKE1" || itemCode == "/LWKE2" || itemCode == "/LCNC1" || itemCode == "/LCNC2")
                {
                    // CNC labor
                    if (m_cnc == null)
                    {
                        m_cnc = componentItem;
                    } else
                    {
                        m_cnc.TotalQuantity += componentItem.TotalQuantity;
                    }
                }
                else if (itemCode == "/LBND2" || itemCode == "/LBND3")
                {
                    // Straight Edgebander labor
                    if (m_ebander == null)
                    {
                        m_ebander = componentItem;
                    } else
                    {
                        m_ebander.TotalQuantity += componentItem.TotalQuantity;
                    }
                }
                else if (itemCode == "/LPNL1" || itemCode == "/LPNL2")
                {
                    // Panel Saw labor
                    if (m_saw == null)
                    {
                        m_saw = componentItem;
                    } else
                    {
                        m_saw.TotalQuantity += componentItem.TotalQuantity;
                    }
                }
                else if (itemCode == "/LCEB1" | itemCode == "/LCEB2")
                {
                    // Contour Edge Bander labor (vector)
                    if (m_vector == null)
                    {
                        m_vector = componentItem;
                    } else
                    {
                        m_vector.TotalQuantity += componentItem.TotalQuantity;
                    }
                }
                else if ( itemCode == "/LATB1" || itemCode == "/LATB2" || itemCode == "/LATB3" || itemCode == "/LACH1" || itemCode == "/LACH2" || itemCode == "/LACH3")
                {
                    // Assembly labor
                    if (m_assm == null)
                    {
                        m_assm = componentItem;
                    } else
                    {
                        m_assm.TotalQuantity += componentItem.TotalQuantity;
                    }
                }
                else if (itemCode == "/LBOX1")
                {
                    // Box construction labor
                    if (m_box == null)
                    {
                        m_box = componentItem;
                    } else
                    {
                        m_box.TotalQuantity += componentItem.TotalQuantity;
                    }
                }
                else if (itemCode.Substring(0, 3) == "006")
                {
                    // Material
                    if (m_material == null)
                    {
                        m_material = componentItem;
                    } else
                    {
                        m_material.TotalQuantity += componentItem.TotalQuantity;
                    }
                }
                else if (itemCode.Substring(0, 2) == "87")
                {
                    // Edgeband
                    if (m_eband == null)
                    {
                        m_eband = componentItem;
                    } else
                    {
                        m_eband.TotalQuantity += componentItem.TotalQuantity;
                    }
                }
                else if (m_box == null && itemCode.Substring(0, 2) == "90")
                {
                    // Paid for box
                    m_boxItemCode = itemCode;
                }
                else
                {
                    // anything else
                    // check the blacklist
                    bool blacklisted = false;
                    foreach (BlacklistItem blItem in m_blacklist )
                    {
                        if (blItem.StartsWith(itemCode))
                        {
                            blacklisted = true;
                            break;
                        }
                    }
                    if (!blacklisted)
                    {
                        // check for existing item first
                        bool foundItem = false;
                        foreach (Item component in m_components)
                        {
                            if (component.ItemCode == itemCode)
                            {
                                foundItem = true;
                                component.TotalQuantity += componentItem.TotalQuantity;
                                break;
                            }
                        }
                        if (!foundItem)
                        {
                            m_components.Add(componentItem);
                        }
                    }
                }
            }
            // Go deeper into each component bill
            foreach (Bill componentBill in bill.ComponentBills)
            {
                componentBill.TotalQuantity = bill.TotalQuantity * componentBill.QuantityPerBill;
                FindComponents(componentBill);
            }
        }
        //check inventory to see how many actually need to be produced.
        public void CheckInventory(OdbcConnection MAS)
        {
            try
            {
                OdbcCommand command = MAS.CreateCommand();
                command.CommandText = "SELECT QuantityOnSalesOrder, QuantityOnHand FROM IM_ItemWarehouse WHERE ItemCode = '" + m_part.BillNo + "'";
                OdbcDataReader reader = command.ExecuteReader();
                if (reader.Read())
                {
                    int available = Convert.ToInt32(reader.GetValue(1)) - Convert.ToInt32(reader.GetValue(0));
                    if (available >= 0)
                    {
                        // No parts need to be produced
                        m_quantity = 0;
                    }
                    else
                    {
                        // adjust the quantity that needs to be produced
                        m_quantity = Math.Min(-available, m_quantity);
                    }
                }
                reader.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occured when accessing inventory: " + ex.Message);
            }
        }
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
        public virtual string Export()
        {
            string json = "";
            json += "{";
            json += "\"ID\":" + '"' + m_ID.ToString() + '"' + ",";
            json += "\"itemCode\":" + '"' + m_part.BillNo + '"' + ",";
            json += "\"quantity\":" + '"' + m_quantity + '"' + ",";
            json += "\"station\":" + '"' + m_station.ToString() + '"' + ",";
            // CHILDREN
            json += "\"children\":[";
            foreach (int child in m_children)
            {
                json += m_children[0] != child ? "," : "";
                json += child.ToString();
            }
            json += "]" + ',';
            // PARENTS
            json += "\"parents\":[";
            foreach (int parent in m_parents)
            {
                json += m_parents[0] != parent ? "," : "";
                json += parent.ToString();
            }
            json += "]" + ',';
            // PARENT ORDERS
            json += "\"parentOrders\":[";
            foreach (string parentOrder in m_parentOrders)
            {
                json += m_parentOrders[0] != parentOrder ? "," : "";
                json += '"' + parentOrder + '"';
            }
            json += "]" + ',';
            // HISTORY
            json += "\"history\":[";
            foreach (Event travelerEvent in m_history)
            {
                json += m_history[0] != travelerEvent ? "," : "";
                json += travelerEvent.Export();
            }
            json += "]";
            json += ExportProperties(); // packs in members specific to derived classes

            json += "}\n";
            return json;
        }
        // export for clients to display
        public virtual string Export(string station) { return ""; }
        public static bool IsTable(string s)
        {
            if (s == null)
            {
                int test = 0;
            }
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
                return "";
            }
            catch (Exception ex)
            {
                return "";
            }
        }
        // sorts the traveler out to its beginning station
        public virtual void Start()
        {
            m_station = Traveler.GetStation("Start");
            m_history.Clear();
            SetNextStation();
            m_station = m_nextStation;
            SetNextStation();
        }
        // advances this traveler to the next station
        public virtual void Advance()
        {
            SetNextStation();
        }
        //-----------------------
        // Private members
        //-----------------------
        
        // overridden in derived classes, packs properties into the Export() json string
        protected virtual string ExportProperties()
        {
            return "";
        }
        protected virtual void SetNextStation()
        {
            m_nextStation = Traveler.GetStation("Start");
        }
        
        //-----------------------
        // Properties
        //-----------------------

        // general
        protected Bill m_part = null;
        protected int m_ID = 0;
        protected string m_timeStamp = "";
        protected bool m_printed = false;
        protected string m_partNo = "";
        protected string m_drawingNo = "";
        protected int m_quantity = 0;
        protected string m_color = "";
        protected int m_station = Traveler.GetStation("Start");
        protected int m_nextStation = Traveler.GetStation("Start");
        protected List<Event> m_history = new List<Event>();
        // relational
        protected List<int> m_children = new List<int>();
        protected List<int> m_parents = new List<int>();
        protected List<string> m_parentOrders = new List<string>();
        // static
        internal static Dictionary<string, int> Stations = new Dictionary<string, int>();
        // Labor
        protected Item m_cnc = null; // labor item
        protected Item m_vector = null; // labor item
        protected Item m_ebander = null; // labor item
        protected Item m_saw = null; // labor item
        protected Item m_assm= null; // labor item
        protected Item m_box = null; // labor item
        // Material
        protected Item m_material = null; // board material
        protected Item m_eband = null; // edgebanding
        protected List<Item> m_components = new List<Item>(); // everything that isn't work, boxes, material or edgebanding
        protected List<BlacklistItem> m_blacklist = new List<BlacklistItem>();
        // Box
        protected int m_partsPerBox = 1;
        protected string m_boxItemCode = "";
        protected string m_regPack = "N/A";
        protected int m_regPackQty = 0;
        protected string m_supPack = "N/A";
        protected int m_supPackQty = 0;

        internal Bill Part
        {
            get
            {
                return m_part;
            }
        }

        internal int ID
        {
            get
            {
                return m_ID;
            }
        }

        internal string TimeStamp
        {
            get
            {
                return m_timeStamp;
            }
            set
            {
                m_timeStamp = value;
            }
        }

        internal bool Printed
        {
            get
            {
                return m_printed;
            }

            set
            {
                m_printed = value;
            }
        }

        internal string PartNo
        {
            get
            {
                return m_partNo;
            }

            set
            {
                m_partNo = value;
            }
        }

        internal string DrawingNo
        {
            get
            {
                return m_drawingNo;
            }

            set
            {
                m_drawingNo = value;
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
                m_part.TotalQuantity = m_quantity;
                FindComponents(m_part);
            }
        }

        internal string Color
        {
            get
            {
                return m_color;
            }

            set
            {
                m_color = value;
            }
        }

        internal Item Cnc
        {
            get
            {
                return m_cnc;
            }

            set
            {
                m_cnc = value;
            }
        }

        internal Item Vector
        {
            get
            {
                return m_vector;
            }

            set
            {
                m_vector = value;
            }
        }

        internal Item Ebander
        {
            get
            {
                return m_ebander;
            }

            set
            {
                m_ebander = value;
            }
        }

        internal Item Saw
        {
            get
            {
                return m_saw;
            }

            set
            {
                m_saw = value;
            }
        }

        internal Item Assm
        {
            get
            {
                return m_assm;
            }

            set
            {
                m_assm = value;
            }
        }

        internal Item Box
        {
            get
            {
                return m_box;
            }

            set
            {
                m_box = value;
            }
        }

        internal Item Material
        {
            get
            {
                return m_material;
            }

            set
            {
                m_material = value;
            }
        }

        internal Item Eband
        {
            get
            {
                return m_eband;
            }

            set
            {
                m_eband = value;
            }
        }

        internal List<Item> Components
        {
            get
            {
                return m_components;
            }

            set
            {
                m_components = value;
            }
        }

        internal List<BlacklistItem> Blacklist
        {
            get
            {
                return m_blacklist;
            }

            set
            {
                m_blacklist = value;
            }
        }
        internal int PartsPerBox
        {
            get
            {
                return m_partsPerBox;
            }

            set
            {
                m_partsPerBox = value;
            }
        }
        internal string BoxItemCode
        {
            get
            {
                return m_boxItemCode;
            }

            set
            {
                m_boxItemCode = value;
            }
        }

        internal string RegPack
        {
            get
            {
                return m_regPack;
            }

            set
            {
                m_regPack = value;
            }
        }

        internal int RegPackQty
        {
            get
            {
                return m_regPackQty;
            }

            set
            {
                m_regPackQty = value;
            }
        }

        internal string SupPack
        {
            get
            {
                return m_supPack;
            }

            set
            {
                m_supPack = value;
            }
        }

        internal int SupPackQty
        {
            get
            {
                return m_supPackQty;
            }

            set
            {
                m_supPackQty = value;
            }
        }

        internal int Station
        {
            get
            {
                return m_station;
            }

            set
            {
                m_station = value;
            }
        }
        internal int NextStation
        {
            get
            {
                return m_nextStation;
            }

            set
            {
                m_nextStation = value;
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

        public List<int> Children
        {
            get
            {
                return m_children;
            }

            set
            {
                m_children = value;
            }
        }

        public List<int> Parents
        {
            get
            {
                return m_parents;
            }

            set
            {
                m_parents = value;
            }
        }
    }
}
