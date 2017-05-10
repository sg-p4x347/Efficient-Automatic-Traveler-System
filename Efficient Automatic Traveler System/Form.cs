using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    public static class Form
    {
        public static string CreateForm(Type objectType, string[]fields)
        {
            Dictionary<string, string> obj = new Dictionary<string, string>() {
                {"name",objectType.Name.Quotate() },
                {"fields",fields.ToList().Stringify() }
            };
            return obj.Stringify();
        }
        private static string Basic(string name, string type, string value)
        {
            Dictionary<string, string> obj = new Dictionary<string, string>() {
                {"type",type.Quotate() },
                {"name",name.Quotate() },
                {"value",value }
            };
            return obj.Stringify();
        }
        public static string Textbox(string name)
        {
            return Basic(name, "text","");
        }
        
        public static string Integer(string name)
        {
            return Basic(name, "number",0.ToString());
        }
        public static string Checkbox(string name)
        {
            return Basic(name, "checkbox",false.ToString().ToLower());
        }
        public static string Selection(string name, string[]options)
        {
            Dictionary<string, string> obj = new Dictionary<string, string>() {
                {"type","select".Quotate() },
                {"name",name.Quotate() },
                {"options",options.ToList().Stringify() }
            };
            return obj.Stringify();
        }
        
    }
}
