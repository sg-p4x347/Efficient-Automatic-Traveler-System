using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Odbc;
using System.Diagnostics;
using System.IO;
using Excel = Microsoft.Office.Interop.Excel;
using Marshal = System.Runtime.InteropServices.Marshal;

namespace Efficient_Automatic_Traveler_System
{
    class ChairManager : TravelerManager
    {
        //-----------------------
        // Public members
        //-----------------------
        public ChairManager() : base(){ }
        public ChairManager(OdbcConnection mas) : base(mas) {

        }
        //-----------------------
        // Private members
        //-----------------------
        protected override void ImportInformation()
        {
            int index = 0;
            foreach (Chair chair in m_travelers.OfType<Chair>())
            {
                if (chair.Part == null) chair.ImportPart(MAS);
                Console.Write("\r{0}%", "Importing Chair Info..." + Convert.ToInt32((Convert.ToDouble(index) / Convert.ToDouble(m_travelers.Count)) * 100));
                chair.CheckInventory(MAS);
                // update and total the final parts
                chair.Part.TotalQuantity = chair.Quantity;
                chair.FindComponents(chair.Part);
                // chair specific
                GetBoxInfo(chair);
            }
            Console.Write("\r{0}", "Importing Chair Info...Finished\n");
        }
        private void GetBoxInfo(Chair traveler)
        {
            if (traveler.PartNo[traveler.PartNo.Length-1] == '4')
            {
                traveler.PartsPerBox = 4;
            } else
            {
                traveler.PartsPerBox = 6;
            }
            traveler.RegPackQty = traveler.Quantity / traveler.PartsPerBox;
        }
        //-----------------------
        // Properties
        //-----------------------
        List<Chair> m_chairs;
    }
}
