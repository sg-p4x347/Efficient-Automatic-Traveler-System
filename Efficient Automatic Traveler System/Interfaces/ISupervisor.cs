using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    interface ISupervisor : ISupervisorActions
    {
        ClientMessage PrintLabel(string json);
        ClientMessage ExportProduction(string json);
        ClientMessage ExportScrap(string json);
    }
}
