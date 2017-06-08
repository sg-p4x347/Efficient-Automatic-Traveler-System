using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    interface ICSV
    {
        Dictionary<string, string> ExportCSV(object parameter = null);
    }
}
