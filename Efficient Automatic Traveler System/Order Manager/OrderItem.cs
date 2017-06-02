using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    class OrderItem
    {
        public OrderItem(Order parent)
        {
            ItemCode = "";
            QtyOrdered = 0;
            QtyOnHand = 0;
            ChildTraveler = -1;
            LineNo = -1;
            Parent = parent;
        }
        public OrderItem(string json, Order parent)
        {
            try
            {
                Parent = parent;
                StringStream ss = new StringStream(json);
                Dictionary<string, string> obj = ss.ParseJSON();
                ItemCode = obj["itemCode"];
                QtyOrdered = Convert.ToInt32(obj["qtyOrdered"]);
                QtyOnHand = Convert.ToInt32(obj["qtyOnHand"]);
                ChildTraveler = Convert.ToInt32(obj["childTraveler"]);
                LineNo = Convert.ToInt32(obj["lineNo"]);
            }
            catch (Exception ex)
            {
                Server.WriteLine("Error while reading OrderItem from file: " + ex.Message);
            }
        }
        public OrderItem(string i, int ordered, int onHand, int c, int l)
        {
            ItemCode = i;
            QtyOrdered = ordered;
            QtyOnHand = onHand;
            ChildTraveler = c;
            LineNo = l;
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"itemCode", ItemCode.ToString().Quotate() },
                {"qtyOrdered", QtyOrdered.ToString() },
                {"qtyOnHand",QtyOnHand.ToString() },
                {"childTraveler",ChildTraveler.ToString() },
                {"lineNo",LineNo.ToString() }
            };
            return obj.Stringify();
        }
        public string ItemCode;
        public int QtyOrdered;
        public int QtyOnHand;
        public int ChildTraveler;
        public int LineNo;
        public Order Parent;
    }
}
