using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Odbc;

namespace Efficient_Automatic_Traveler_System
{
    class Bill
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
            m_componentBills = bill.ComponentBills;
            m_componentItems = bill.ComponentItems;
        }
        public Bill(string billNo, double quantityPerBill)
        {
            m_billNo = billNo;
            m_quantityPerBill = quantityPerBill;
        }
        public Bill(string billNo, double quantityPerBill, OdbcConnection MAS)
        {
            m_billNo = billNo;
            m_quantityPerBill = quantityPerBill;
            Import(MAS);
        }
        public void Import(OdbcConnection MAS)
        {
            if (!m_imported)
            {
                try
                {
                    // get bill information from MAS
                    {
                        OdbcCommand command = MAS.CreateCommand();
                        command.CommandText = "SELECT BillType, BillDesc1, CurrentBillRevision, DrawingNo, Revision FROM BM_billHeader WHERE billno = '" + m_billNo + "'";
                        OdbcDataReader reader = command.ExecuteReader();
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
                        reader.Close();
                    }
                    // add the components from MAS
                    {
                        OdbcCommand command = MAS.CreateCommand();
                        command.CommandText = "SELECT ItemType, BillType, Revision, ComponentItemCode, QuantityPerBill FROM BM_billDetail WHERE billno = '" + m_billNo + "'";
                        OdbcDataReader reader = command.ExecuteReader();
                        // begin to read
                        while (reader.Read())
                        {
                            // exclude items of type '4' (comments) and revision numbers that don't match the bill's revision number
                            if (reader.GetInt32(0) != 4 && m_currentBillRevision == reader.GetString(2))
                            {
                                // determine if the component has a bill
                                if (!reader.IsDBNull(1))
                                {
                                    // Component has a bill
                                    m_componentBills.Add(new Bill(reader.GetString(3), reader.GetDouble(4), MAS));
                                }
                                else
                                {
                                    // Component is an item
                                    m_componentItems.Add(new Item(reader.GetString(3), reader.GetDouble(4), MAS));
                                }
                            }
                        }
                        reader.Close();
                    }
                    // success
                    m_imported = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occured when retrieving Bill information from MAS: " + ex.Message);
                }
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
        // Properties
        private bool m_imported = false;
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

        public char BillType1
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

        internal List<Item> ComponentItems
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

        internal List<Bill> ComponentBills
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
    }
}
