using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Quick_Ship_Router
{
    class Order
    {
        private DateTime orderDate = DateTime.Today;
        private string salesOrderNo = "";
        private string customerNo = "";
        private string itemCode  = "";
        private string productLine = "";
        private int quantityOrdered = 0;
        private string shipVia = "";
        public Order()
        {

        }
        public Order(string json)
        {
            try
            {
                bool readString = false;
                string stringToken = "";

                string memberName = "";
                bool readMember = false;

                string value = "";
                bool readValue = false;
                // SalesOrderNo
                for (int pos = 0; pos < json.Length; pos++)
                {
                    char ch = json[pos];
                    switch (ch) {
                        case '"':
                            readString = !readString;
                            continue;
                        case ':':
                            memberName = stringToken; stringToken = "";
                            continue;
                        case ',':
                        case '}':
                            value = stringToken; stringToken = "";
                            // set the corresponding member
                            if (memberName == "salesOrderNo")
                            {
                                salesOrderNo = value;
                            }
                            else if (memberName == "customerNo")
                            {
                                customerNo = value;
                            }
                            else if (memberName == "itemCode")
                            {
                                itemCode = value;
                            }
                            else if (memberName == "productLine")
                            {
                                productLine = value;
                            }
                            else if (memberName == "quantityOrdered")
                            {
                                quantityOrdered = Convert.ToInt32(value);
                            }
                            else if (memberName == "shipVia")
                            {
                                shipVia = value;
                            }
                            continue;
                    }
                    if (readString)
                    {
                        // read string character by character
                        stringToken += ch;
                    }
                }
            } catch (Exception ex)
            {
                MessageBox.Show("Problem reading in order from printed.txt: " + ex.Message);
            }
        }
        public string Export()
        {
            string doc = "";
            doc += "{";
            doc += "\"salesOrderNo\":" + '"' + salesOrderNo + '"' + ",";
            doc += "\"customerNo\":" + '"' + customerNo + '"' + ",";
            doc += "\"itemCode\":" + '"' + itemCode + '"' + ",";
            doc += "\"productLine\":" + '"' + productLine + '"' + ",";
            doc += "\"quantityOrdered\":" + '"' + quantityOrdered + '"' + ",";
            doc += "\"shipVia\":" + '"' + shipVia + '"';
            doc += "}";
            return doc;
        }
        public DateTime OrderDate
        {
            get
            {
                return orderDate;
            }

            set
            {
                orderDate = value;
            }
        }

        public string SalesOrderNo
        {
            get
            {
                return salesOrderNo;
            }

            set
            {
                salesOrderNo = value;
            }
        }

        public string CustomerNo
        {
            get
            {
                return customerNo;
            }

            set
            {
                customerNo = value;
            }
        }

        public string ItemCode
        {
            get
            {
                return itemCode;
            }

            set
            {
                itemCode = value;
            }
        }

        public int QuantityOrdered
        {
            get
            {
                return quantityOrdered;
            }

            set
            {
                quantityOrdered = value;
            }
        }

        public string ShipVia
        {
            get
            {
                return shipVia;
            }

            set
            {
                shipVia = value;
            }
        }

        public string ProductLine
        {
            get
            {
                return productLine;
            }

            set
            {
                productLine = value;
            }
        }
    }
}
