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
            QtyShipped = 0;
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
                QtyOrdered = obj.ContainsKey("qtyShipped") ? Convert.ToInt32(obj["qtyShipped"]) : 0;
                QtyOnHand = Convert.ToInt32(obj["qtyOnHand"]);
                ChildTraveler = Convert.ToInt32(obj["childTraveler"]);
                LineNo = Convert.ToInt32(obj["lineNo"]);
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
            }
        }
        public OrderItem(string i, int ordered, int shipped, int onHand, int c, int l)
        {
            ItemCode = i;
            QtyOrdered = ordered;
            QtyShipped = shipped;
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
                {"qtyShipped",QtyShipped.ToString() },
                {"qtyOnHand",QtyOnHand.ToString() },
                {"childTraveler",ChildTraveler.ToString() },
                {"lineNo",LineNo.ToString() }
            };
            return obj.Stringify();
        }
        public string ItemCode;
        public int QtyOrdered;
        public int QtyShipped;
        public int QtyOnHand;
        public int ChildTraveler;
        public int LineNo;
        public Order Parent;

        public int QtyNeeded
        {
            get
            {
                return QtyOrdered - QtyShipped;
            }
        }
    }
}
