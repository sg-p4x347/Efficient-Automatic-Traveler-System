using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Odbc;
using System.Runtime.ExceptionServices;

namespace Efficient_Automatic_Traveler_System
{
    public class Item
    {
        // Interface
        public Item(string itemCode, double quantityPerBill, double parentQuantity, OdbcConnection MAS)
        {
            try
            {
                m_itemCode = itemCode;
                m_quantityPerBill = quantityPerBill;
                m_totalQuantity = m_quantityPerBill * parentQuantity;
                Import(MAS);
            }
            catch (Exception ex)
            {
                Server.WriteLine("An error occured when retrieving item information from MAS: " + ex.Message);
            }
        }
        public void Clone(Item item)
        {
            m_itemCodeDesc = item.ItemCodeDesc;
            Unit = item.Unit;
        }
        [HandleProcessCorruptedStateExceptions]
        public async Task Import(OdbcConnection MAS)
        {
            try
            {
                Item existing = m_items.Find(b => b.ItemCode == ItemCode);
                if (existing != null)
                {
                    Clone(existing);
                }
                else
                {
                    // get item info from MAS
                    if (MAS.State != System.Data.ConnectionState.Open) throw new Exception("MAS is in a closed state!");
                    OdbcCommand command = MAS.CreateCommand();
                    command.CommandText = "SELECT ItemCodeDesc, StandardUnitOfMeasure FROM CI_item WHERE itemCode = '" + m_itemCode + "'";
                    OdbcDataReader reader = (OdbcDataReader)(command.ExecuteReader(System.Data.CommandBehavior.SingleRow));

                    // begin to read
                    if (reader.Read())
                    {
                        //if (!reader.IsDBNull(0)) m_itemType = reader.GetInt32(0);
                        if (!reader.IsDBNull(0)) m_itemCodeDesc = reader.GetString(0);
                        if (!reader.IsDBNull(1)) m_unit = reader.GetString(1);
                    }
                    reader.Close();
                    m_items.Add(this);
                }
            }
            catch (AccessViolationException ex)
            {
                Server.HandleODBCexception(ex);
                await Import(MAS);
            } catch (Exception ex)
            {
                Server.LogException(ex);
                await Import(MAS);
            }
        }
        public Item (Item item)
        {
            m_itemCode = item.ItemCode;
            m_itemCodeDesc = item.ItemCodeDesc;
            m_quantityPerBill = item.QuantityPerBill;
            m_unit = item.Unit;
        }
        //public string Export(string name)
        //{
        //    NameValueQty<string, string> nvq = new NameValueQty<string, string>(name,;
        //}
        // Properties
        private string m_itemCode;
        private string m_itemCodeDesc;
        private double m_quantityPerBill;
        private double m_totalQuantity;
        private string m_unit;

        // item cache
        private static List<Item> m_items = new List<Item>();
        public string ItemCode
        {
            get
            {
                return m_itemCode;
            }

            set
            {
                m_itemCode = value;
            }
        }

        public string ItemCodeDesc
        {
            get
            {
                return m_itemCodeDesc;
            }

            set
            {
                m_itemCodeDesc = value;
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
