using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    interface IManager
    {
        // Imports everything from the date (defaults to today)
        void Import(DateTime? date = null);
        // Imports the most recent data excluding items deemed "old"
        void ImportPast();
        // Backs up data to the current day's backup folder
        void Backup();
    }
}
