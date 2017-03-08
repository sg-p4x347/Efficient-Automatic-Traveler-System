using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Odbc;

namespace Efficient_Automatic_Traveler_System
{
    class Item
    {
        // Interface
        public Item(string itemCode, double quantityPerBill, ref OdbcConnection MAS)
        {
            try
            {
                m_itemCode = itemCode;
                m_quantityPerBill = quantityPerBill;
                // get item info from MAS
                if (MAS.State != System.Data.ConnectionState.Open) throw new Exception("MAS is in a closed state!");
                OdbcCommand command = MAS.CreateCommand();
                command.CommandText = "SELECT ItemCodeDesc, StandardUnitOfMeasure FROM CI_item WHERE itemCode = '" + itemCode + "'";
                OdbcDataReader reader = command.ExecuteReader();

                // begin to read
                if (reader.Read())
                {
                
                        //if (!reader.IsDBNull(0)) m_itemType = reader.GetInt32(0);
                        if (!reader.IsDBNull(0)) m_itemCodeDesc = reader.GetString(0);
                        if (!reader.IsDBNull(1)) m_unit = reader.GetString(1);
                
                }
                reader.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occured when retrieving item information from MAS: " + ex.Message);
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
