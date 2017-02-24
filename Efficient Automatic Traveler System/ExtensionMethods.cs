using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    public static class ExtensionMethods
    {
        public static DateTime RoundUp(this DateTime dt, TimeSpan d)
        {
            return new DateTime(((dt.Ticks + d.Ticks - 1) / d.Ticks) * d.Ticks);
        }
        public static bool HasMethod(this object objectToCheck, string methodName)
        {
            try
            {
                var type = objectToCheck.GetType();
                return type.GetMethod(methodName) != null;
            }
            catch (Exception ex)
            {
                // ambiguous means there is more than one result,
                // which means: a method with that name does exist
                return true;
            }
        }
        public static string Stringify<itemType>(this List<itemType> list)
        {
            string json = "[";
            foreach (itemType s in list)
            {
                if (json.Length > 1) json += ',';
                if (typeof(itemType) == typeof(string))
                {
                    json += '"' + s.ToString() + '"';
                } else
                {
                    json += s.ToString();
                }
                
            }
            json += "]";
            return json;
        }
    }
}
