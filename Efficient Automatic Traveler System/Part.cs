using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    public class Part : Traveler
    {
        #region Public members
        public Part() : base() {
            m_bill = null;
        }
        public Part(Form form) : base(form)
        {
            m_bill = new Bill(form.ValueOf("itemCode"), 1, Convert.ToInt32(form.ValueOf("quantity")));
           
        }
        public Part(string json) : base(json) {
            Dictionary<string, string> obj = new StringStream(json).ParseJSON();
            if (obj["itemCode"] != "")
            {
                m_bill = new Bill(obj["itemCode"], 1, m_quantity);
            }
        }
        public Part(string itemCode, int quantity) : base(itemCode, quantity) {
            m_bill = new Bill(itemCode, 1, quantity);
        }
        public override void AdvanceItem(ushort ID, ITravelerManager travelerManager = null)
        {
            TravelerItem item = FindItem(ID);
            item.Station = GetNextStation(ID);
        }

        public override bool CombinesWith(object[] args)
        {
            return ItemCode == (string)args[0];
        }

        public override string ExportTableRows(StationClass station)
        {
            return base.ExportTableRows(station);
        }

        public override double GetCurrentLabor(StationClass station = null)
        {
            throw new NotImplementedException();
        }

        public override string GetLabelFields(ushort itemID, LabelType type)
        {
            throw new NotImplementedException();
        }

        public override StationClass GetNextStation(ushort itemID)
        {
            TravelerItem item = FindItem(itemID);
            Bill currentBill = BillOf(item.ItemCode);
            // this list of stations (in order) in the the item's current bill
            List<StationClass> stations = StationClass.StationsInBill(currentBill);
            int index = stations.IndexOf(stations.First(s => s == item.Station));
            if (index + 1 < stations.Count)
            {
                // go to the next station in this bill (labor code)
                return stations[index + 1];
            }
            else
            {
                // reached the end of this bill's stations (labor codes)

                // advance to an outer bill
                Bill outer = currentBill.Parent;
                if (outer != null && outer != Bill)
                {
                    return StationClass.StationsInBill(outer).First();
                }
                else
                {
                    // no more bills for this part, it is complete
                    return StationClass.GetStation("Finished");
                }
            }
        }

        public override double GetTotalLabor(StationClass station = null)
        {
            throw new NotImplementedException();
        }

        public override void ImportInfo(ITravelerManager travelerManager, IOrderManager orderManager, OdbcConnection MAS)
        {
            m_bill.Import(MAS);
        }
        public virtual bool HasDrawing()
        {
            // check the drawings directory for a drawing
            return (System.IO.File.Exists(ConfigManager.Get("drawings") + Bill.DrawingNo + ".pdf"));
        }
        #endregion
        #region Private Methods
        private Bill BillOf(string itemCode)
        {
            Bill childBill = Bill.ComponentBills.FirstOrDefault();
            if (childBill != null)
            {
                if (childBill.BillNo == itemCode)
                {
                    return childBill;
                } else
                {
                    return BillOf(childBill.BillNo);
                }
            } else
            {
                return null;
            }
        }
        #endregion
        #region Properties
        private Bill m_bill;
        #endregion
        #region Interface
        public Bill Bill
        {
            get
            {
                return m_bill;
            }
            protected set
            {
                m_bill = value;
            }
        }
        #endregion
    }
}
