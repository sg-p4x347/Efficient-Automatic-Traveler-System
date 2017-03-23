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
        // turns a list into a json string: [obj.ToString(),obj.ToString(),...]
        public static string Stringify<itemType>(this List<itemType> list, bool quotate = true)
        {
            string json = "[";
            if (list != null)
            {
                foreach (itemType s in list)
                {
                    if (json.Length > 1) json += ',';
                    if (quotate && typeof(itemType) == typeof(string))
                    {
                        json += '"' + s.ToString() + '"';
                    }
                    else
                    {
                        json += s.ToString();
                    }

                }
            }
            json += "]";
            return json;
        }
        // returns a JSON string representing the collection of name value pairs
        public static string Stringify(this Dictionary<string,string> obj,bool pretty = false)
        {
            string json = "{";
            bool first = true;
            foreach (KeyValuePair<string,string> pair in obj)
            {
                if (pretty) json += "\n\t";
                json += (first ? "" : ",") + pair.Key.Quotate() + ':' + pair.Value;
                first = false;
            }
            if (pretty) json += '\n';
            json += '}';
            return json;
        }
        // calling ToString on a string should return a quoted string, for JSON formatting
        public static string Quotate(this string s)
        {
            return '"' + s + '"';
        }
    }
}
