using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    public interface IForm
    {
        Form CreateForm();
        Form CreateFilledForm();
        void Update(Form form);
    }
}
