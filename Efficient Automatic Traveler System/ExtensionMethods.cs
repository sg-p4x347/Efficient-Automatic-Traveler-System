﻿using System;
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
        public static string Stringify<itemType>(this List<itemType> list, bool quotate = true, bool pretty = false)
        {
            string json = "[";
            if (list != null)
            {
                bool first = true;
                foreach (itemType s in list)
                {
                    json += (first ? "" : ",") + (pretty ? Environment.NewLine + '\t' : "");
                    if (quotate && typeof(itemType) == typeof(string))
                    {
                        json += s.ToString().Quotate();
                    }
                    else
                    {
                        json += s.ToString();
                    }
                    first = false;
                }
            }
            if (pretty) json += Environment.NewLine;
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
                json += (first ? "" : ",") + (pretty ? Environment.NewLine + '\t' : "") + pair.Key.Quotate() + ':' + pair.Value;
                first = false;
            }
            if (pretty) json += Environment.NewLine;
            json += '}';
            return json;
        }
        // calling ToString on a string should return a quoted string, for JSON formatting
        public static string Quotate(this string s)
        {
            return '"' + s + '"';
        }
        // returns a JSON string representing the collection of enumeration values
        public static string Stringify<T>()
        {
            
            return GetNames<T>().Stringify<string>();
        }
        public static List<string> GetNames<T>()
        {
            Type enumType = typeof(T);

            // Can't use type constraints on value types, so have to do check like this
            if (enumType.BaseType != typeof(Enum))
                throw new ArgumentException("T must be of type System.Enum");

            Array enumValArray = Enum.GetValues(enumType);
            List<string> names = new List<string>();

            foreach (T val in enumValArray)
            {

                names.Add(val.ToString());
            }
            return names;
        }
        // returns the list of names that are less than the enumeration value
        public static List<string> GetNamesLessThanOrEqual<T>(T less)
        {
            Type enumType = typeof(T);

            // Can't use type constraints on value types, so have to do check like this
            if (enumType.BaseType != typeof(Enum))
                throw new ArgumentException("T must be of type System.Enum");

            Array enumValArray = Enum.GetValues(enumType);
            List<string> names = new List<string>();

            foreach (T val in enumValArray)
            {
                Enum value = Enum.Parse(enumType, val.ToString()) as Enum;
                Enum lessValue = Enum.Parse(enumType, less.ToString()) as Enum;
                if (value.CompareTo(lessValue) <= 0)
                {
                    names.Add(val.ToString());
                }
            }
            return names;
        }
    }
}
