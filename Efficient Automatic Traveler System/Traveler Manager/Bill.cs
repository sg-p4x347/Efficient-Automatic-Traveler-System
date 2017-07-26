using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Odbc;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Efficient_Automatic_Traveler_System
{
    public class Bill
    {
        //enum BillType
        //{
        //    Standard, Phantom, Engineering, Inactive, Kit
        //}
        // Interface
        public Bill(Bill bill)
        {
            m_billNo = bill.BillNo;
            m_quantityPerBill = bill.QuantityPerBill;
            m_billDesc = bill.BillDesc;
            DrawingNo = bill.DrawingNo;
            CurrentBillRevision = bill.CurrentBillRevision;
            m_componentBills = bill.ComponentBills;
            m_componentItems = bill.ComponentItems;
        }
        public void Clone(Bill bill)
        {
            m_billNo = bill.BillNo;
            m_quantityPerBill = bill.QuantityPerBill;
            m_billDesc = bill.BillDesc;
            DrawingNo = bill.DrawingNo;
            CurrentBillRevision = bill.CurrentBillRevision;
            m_componentBills = bill.ComponentBills;
            m_componentItems = bill.ComponentItems;
        }
        public Bill(string billNo, double quantityPerBill, double parentQuantity)
        {
            m_billNo = billNo;
            m_quantityPerBill = quantityPerBill;
            m_totalQuantity = parentQuantity * m_quantityPerBill;
        }
        public Bill(string billNo, double quantityPerBill, double parentQuantity,OdbcConnection MAS,Bill parent)
        {
            m_billNo = billNo;
            m_quantityPerBill = quantityPerBill;
            m_totalQuantity = parentQuantity * m_quantityPerBill;
            m_parent = parent;
            Import(MAS);
        }
        
        public void Import(OdbcConnection MAS)
        {
            Bill existing = m_bills.Find(b => b.BillNo == BillNo);
            if (existing != null) {
                Clone(existing);
            }
            else if (!Imported)
            {
                // Import header
                if (!HeaderImported)
                {
                    var tokenSource = new CancellationTokenSource();

                    var headerTask = Task.Run(() => ImportHeader(MAS), tokenSource.Token);
                    if (!headerTask.Wait(TimeSpan.FromSeconds(3)) || !HeaderImported)
                    {
                        // Trying again
                        tokenSource.Cancel();
                        Server.WriteLine("-Bill header timed out, trying again-");
                        Import(MAS);
                    }
                }
                // Import detail
                if (!DetailImported)
                {
                    var tokenSource = new CancellationTokenSource();

                    var detailTask = Task.Run(() => ImportDetail(MAS), tokenSource.Token);
                    if (!detailTask.Wait(TimeSpan.FromSeconds(3)) || !DetailImported)
                    {
                        // Trying again
                        tokenSource.Cancel();
                        Server.WriteLine("-Bill detail import timed out, trying again-");
                        Import(MAS);
                    }
                }
                m_bills.Add(this);
            }
        }
        private bool IsImported(OdbcConnection MAS)
        {
            if (!Imported)
            {
                Server.WriteLine("-Bill import timed out-");
            }
            return Imported;
        }
        [HandleProcessCorruptedStateExceptions]
        private void ImportHeader(OdbcConnection MAS)
        {
            try
            {
                // get bill information from MAS
                if (MAS.State != System.Data.ConnectionState.Open) throw new Exception("MAS is in a closed state!");
                
                OdbcCommand command = MAS.CreateCommand();
                command.CommandTimeout = 30;
                command.CommandText = "SELECT BillType, BillDesc1, CurrentBillRevision, DrawingNo, Revision FROM BM_billHeader WHERE billno = '" + m_billNo + "'";

                OdbcDataReader reader = (OdbcDataReader)(command.ExecuteReader(System.Data.CommandBehavior.SequentialAccess));
                // read info
                while (reader.Read())
                {
                    string currentRev = reader.GetString(4);
                    string thisRev = reader.GetString(2);
                    // only use the current bill revision
                    if (currentRev == thisRev) // if (current bill revision == this revision)
                    {
                        m_billType = reader.GetString(0)[0];
                        m_billDesc = reader.GetString(1);
                        m_currentBillRevision = reader.GetString(2);
                        if (!reader.IsDBNull(3))
                        {
                            m_drawingNo = reader.GetString(3);
                        }
                        break;
                    }
                }
                // Success!
                HeaderImported = true;
                reader.Close();
            }
            catch (AccessViolationException ex)
            {
                Server.HandleODBCexception(ex);
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
            }
        }
        // add the components from MAS
        [HandleProcessCorruptedStateExceptions]
        private void ImportDetail(OdbcConnection MAS)
        {
            try
            {
                if (MAS.State != System.Data.ConnectionState.Open) throw new Exception("MAS is in a closed state!");
                OdbcCommand command = MAS.CreateCommand();
                command.CommandText = "SELECT \"ItemType\", \"BillType\", \"Revision\", \"ComponentItemCode\", \"QuantityPerBill\" FROM \"BM_billDetail\" WHERE \"billno\" = '" + m_billNo + "'";

                using (OdbcDataReader reader = command.ExecuteReader())
                {
                    
                    // begin to read
                    while (reader.Read())
                    {
                        // Success for this detail
                        DetailImported = true;
                        // exclude items of type '4' (comments) and revision numbers that don't match the bill's revision number
                        if (reader.GetInt32(0) != 4 && m_currentBillRevision == reader.GetString(2))
                        {
                            // determine if the component has a bill
                            if (!reader.IsDBNull(1))
                            {
                                // Component has a bill
                                m_componentBills.Add(new Bill(reader.GetString(3), reader.GetDouble(4), m_totalQuantity, MAS, this));
                            }
                            else
                            {
                                // Component is an item
                                m_componentItems.Add(new Item(reader.GetString(3), reader.GetDouble(4), m_totalQuantity, MAS));
                            }
                        }
                    }
                }
            }
            catch (AccessViolationException ex)
            {
                Server.HandleODBCexception(ex);
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
            }
        }
        // Find components, returns true if found, false if not found
        public bool SearchItem(string itemCode)
        {
            foreach (Item componentItem in m_componentItems)
            {
                if (componentItem.ItemCode == itemCode)
                {
                    return true;
                }
            }
            return false;
        }
        // return the labor(min) at the station as a double 
        public double LaborAt(StationClass station)
        {
            Item item = ComponentItems.Find(i => station.LaborCodes.Contains(i.ItemCode));
            if (item != null)
            {
                return item.QuantityPerBill;
            } else
            {
                return 0.0;
            }
        }
        // Properties
        private bool m_imported = false;
        private bool m_headerImported = false;
        private bool m_detailImported = false;
        private string m_billNo = "";
        private string m_drawingNo = "";
        private double m_quantityPerBill = 0.0;
        private double m_totalQuantity = 0.0;
        private char m_billType = 'S';
        private string m_billDesc;
        private string m_currentBillRevision;
        
        private string m_unit;
        // components
        private List<Item> m_componentItems = new List<Item>();
        private List<Bill> m_componentBills = new List<Bill>();
        // parent bill
        private Bill m_parent = null;

        // store a cache of all loaded bills
        private static List<Bill> m_bills = new List<Bill>();
        public string Unit
        {
            get
            {
                return m_unit;
            }

            set
            {
                m_unit = value;
            }
        }

        public bool Imported
        {
            get
            {
                return m_imported;
            }

            set
            {
                m_imported = value;
            }
        }

        public string BillNo
        {
            get
            {
                return m_billNo;
            }

            set
            {
                m_billNo = value;
            }
        }

        public double QuantityPerBill
        {
            get
            {
                return m_quantityPerBill;
            }

            set
            {
                m_quantityPerBill = value;
            }
        }

        public char BillType
        {
            get
            {
                return m_billType;
            }

            set
            {
                m_billType = value;
            }
        }

        public string BillDesc
        {
            get
            {
                return m_billDesc;
            }

            set
            {
                m_billDesc = value;
            }
        }

        public string CurrentBillRevision
        {
            get
            {
                return m_currentBillRevision;
            }

            set
            {
                m_currentBillRevision = value;
            }
        }

        public string Unit1
        {
            get
            {
                return m_unit;
            }

            set
            {
                m_unit = value;
            }
        }

        public List<Item> ComponentItems
        {
            get
            {
                return m_componentItems;
            }

            set
            {
                m_componentItems = value;
            }
        }

        public List<Bill> ComponentBills
        {
            get
            {
                return m_componentBills;
            }

            set
            {
                m_componentBills = value;
            }
        }

        public string DrawingNo
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

        public double TotalQuantity
        {
            get
            {
                return m_totalQuantity;
            }

            set
            {
                m_totalQuantity = value;
            }
        }

        public Bill Parent
        {
            get
            {
                return m_parent;
            }

            set
            {
                m_parent = value;
            }
        }

        public bool HeaderImported
        {
            get
            {
                return m_headerImported;
            }

            set
            {
                m_headerImported = value;
            }
        }

        public bool DetailImported
        {
            get
            {
                return m_detailImported;
            }

            set
            {
                m_detailImported = value;
            }
        }
    }
}
