using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    abstract class JSON
    {
        public static JSON Parse(string json)
        {
            StringStream stream = new StringStream(json);
            return Import(ref stream);
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
                        case '/': GetCommentScope(ref json); continue;
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
                            } else
                            {
                                return new JsonNull();
                            }
                    }
                }
            }
            return null;
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
                        case ',':
                        case '/': json.PutBack(); return primitive;
                        default: primitive += ch; break;
                    }
                }
            }
            return primitive;
        }
    }
}
