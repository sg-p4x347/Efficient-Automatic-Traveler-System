using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    class OrderItem
    {
        public OrderItem()
        {
            ItemCode = "";
            QtyOrdered = 0;
            QtyOnHand = 0;
            ChildTraveler = -1;
        }
        public OrderItem(string json)
        {
            try
            {
                StringStream ss = new StringStream(json);
                Dictionary<string, string> obj = ss.ParseJSON();
                ItemCode = obj["itemCode"];
                QtyOrdered = Convert.ToInt32(obj["qtyOrdered"]);
                QtyOnHand = Convert.ToInt32(obj["qtyOnHand"]);
                ChildTraveler = Convert.ToInt32(obj["childTraveler"]);
            }
            catch (Exception ex)
            {
                Server.WriteLine("Error while reading OrderItem from file: " + ex.Message);
            }
        }
        public OrderItem(string i, int ordered, int onHand, int c)
        {
            ItemCode = i;
            QtyOrdered = ordered;
            QtyOnHand = onHand;
            ChildTraveler = c;
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"itemCode", ItemCode.ToString().Quotate() },
                {"qtyOrdered", QtyOrdered.ToString() },
                {"qtyOnHand",QtyOnHand.ToString() },
                {"childTraveler",ChildTraveler.ToString() }
            };
            return obj.Stringify();
        }
        public string ItemCode;
        public int QtyOrdered;
        public int QtyOnHand;
        public int ChildTraveler;
    }
}
