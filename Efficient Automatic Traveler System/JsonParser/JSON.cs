using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    public abstract class JSON
    {
        public static JSON Parse(string json)
        {
            StringStream stream = new StringStream(json);
            return Import(ref stream);
        }
        // From JSON to primitives
        static public implicit operator int(JSON value)
        {
            return Convert.ToInt32(value.Value);
        }
        static public implicit operator double(JSON value)
        {
            return Convert.ToInt32(value.Value);
        }
        static public implicit operator bool(JSON value)
        {
            return Convert.ToBoolean(value.Value);
        }
        static public implicit operator string(JSON value)
        {
            return value.ToString().DeQuote();
        }
        // From primitives to JSON
        static public implicit operator JSON(int value)
        {
            return new JsonInt(value);
        }
        static public implicit operator JSON(double value)
        {
            return new JsonDouble(value);
        }
        static public implicit operator JSON(bool value)
        {
            return new JsonBool(value);
        }
        static public implicit operator JSON(string value)
        {
            return new JsonString(value);
        }
        public JSON this[int index]
        {
            get { return (this as JsonArray)[index]; }
            set { (this as JsonArray)[index] = value; }
        }
        public JSON this[string key]
        {
            get { return (this as JsonObject)[key]; }
            set { (this as JsonObject)[key] = value; }
        }
        public T ToEnum<T>()
        {
            Type enumType = typeof(T);

            // Can't use type constraints on value types, so have to do check like this
            if (enumType.BaseType != typeof(Enum))
                throw new ArgumentException("T must be of type System.Enum");

            return (T)Enum.Parse(enumType, Value.ToString().DeQuote());
        }
        public override string ToString()
        {
            return Value != null ? Value.ToString() : "";
        }
        private object m_value;

        protected object Value
        {
            get
            {
                return m_value;
            }

            set
            {
                this.m_value = value;
            }
        }

        protected static JSON Import(ref StringStream json)
        {
            char ch = ' ';
            while (json.Get(ref ch))
            {
                if (!Char.IsWhiteSpace(ch))
                {
                    switch (ch)
                    {
                        case '{': return new JsonObject(ref json);
                        case '[': return new JsonArray(ref json);
                        case '"': json.PutBack(); return new JsonString(ref json);
                        case '/':
                            if (json.Get(ref ch) && ch == '/')
                            {
                                GetCommentScope(ref json); continue;
                            } else
                            {
                                json.PutBack();
                                goto default;
                            }
                        default:
                            
                            json.PutBack();
                            string primitive = GetPrimitiveScope(ref json);
                            int integer = 0;
                            double floating = 0.0;
                            bool boolean = false;
                            if (Int32.TryParse(primitive, out integer))
                            {
                                return new JsonInt(integer);
                            }
                            else if (Double.TryParse(primitive, out floating))
                            {
                                return new JsonDouble(floating);
                            } else if (Boolean.TryParse(primitive, out boolean))
                            {
                                return new JsonBool(boolean);
                            } else if (primitive == "null" || primitive == "undefined")
                            {
                                return new JsonNull();
                            } else
                            {
                                return new JsonString(primitive);
                            }
                    }
                }
            }
            return null;
        }
        public string Humanize()
        {
            string json = ToString();
            string formatted = "";
            int scopeLevel = 0;
            foreach (Char ch in json)
            {
                if (ch == '}' || ch == ']')
                {
                    scopeLevel--;
                } else
                {
                    formatted += ch;
                    if (ch == '{' || ch == '[')
                    {
                        scopeLevel++;
                    }
                }
                if (new char[] { '{', '}', '[',']',','}.Contains(ch))
                {

                    formatted += Environment.NewLine;
                    formatted += new string('\t', scopeLevel);
                    if (ch == '}' || ch == ']')
                    {
                        formatted += ch;
                    }
                }
            }
            return formatted;
        }
        private static void GetCommentScope(ref StringStream json) {
            char ch = ' ';
	        while (json.Get(ref ch)) {
		        if (ch == '\n') return;
	        }
        }
        private static string GetPrimitiveScope(ref StringStream json)
        {
            string primitive = "";
            char ch = ' ';
            while (json.Get(ref ch)) {
                if (!Char.IsWhiteSpace(ch))
                {
                    switch (ch)
                    {
                        case '}':
                        case ']':
                        case ',': json.PutBack(); return primitive;
                        //case '/': break;
                        default: primitive += ch; break;
                    }
                }
            }
            return primitive;
        }
    }
}
