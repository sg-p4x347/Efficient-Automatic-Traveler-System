using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    class StringStream
    {
        public StringStream(string s)
        {
            m_string = s;
            m_position = -1;
        }
        public Dictionary<string,string> ParseJSON(bool dequote = true)
        {
            Dictionary<string, string> obj = new Dictionary<string, string>();
            // find the start of the key name
            string key = "";
            char token = ' ';
            while (Get(ref token))
            {
                if (token == '"')
                {
                    PutBack();
                    key = GetJsonScope().Trim('"');
                } else if (token == ':')
                {
                    if (!obj.ContainsKey(key))
                    {
                        // adding the key with the value (obtained from getting the next json scope)
                        if (dequote)
                        {
                            obj.Add(key, GetJsonScope().Trim('"')); 
                        } else
                        {
                            obj.Add(key, GetJsonScope());
                        }
                        
                    }
                }
            }
            return obj;
        }
        public List<string> ParseJSONarray(bool dequote = true)
        {
            List<string> array = new List<string>();
            // find the start
            char opening = ' ';
            while (Get(ref opening))
            {
                switch (opening)
                {
                    case '[':
                    case '{':
                        goto begin;
                }
            }
        begin:
            char next = ' ';
            while (Get(ref next))
            {
                if (next == '[' || next == '{' || next == '"' || Char.IsNumber(next) || next == '-')
                {
                    PutBack();
                    if (dequote)
                    {
                        array.Add(GetJsonScope().Trim('"'));
                    } else
                    {
                        array.Add(GetJsonScope());
                    }
                }
            }
            return array;
        }
        private string GetJsonScope()
        {
            string scope = "";
            // find the start
            char opening = ' ';
            char closing = ' ';
            while (Get(ref opening))
            {
                switch (opening)
                {
                    case '[':
                        closing = ']';
                        scope += opening;
                        goto begin;
                    case '{':
                        closing = '}';
                        scope += opening;
                        goto begin;
                    case '"':
                        closing = '"';
                        scope += opening;
                        goto begin;
                    case 't':
                    case 'f':
                        closing = 'e';
                        scope += opening;
                        goto begin;
                    default:
                        if (Char.IsNumber(opening) || opening == '-')
                        {
                            PutBack();
                            goto begin;
                        }
                        break;
                }
            }
            return "";
            begin:
            char ch = ' ';
            while (Get(ref ch))
            {
                if (ch == '[' || ch == '{' || (closing != '"' && ch == '"'))
                {
                    PutBack();
                    scope += GetJsonScope(); // get the inner scope and add it to the current scope
                }
                else if (ch != '\n')
                {
                    scope += ch; // otherwise just add this character to the scope string
                }
                if (ch == closing)
                {
                    return scope; // done!
                } else if (Char.IsNumber(opening) || opening == '-')
                {
                    // for numbers
                    char next = ' ';
                    Get(ref next);
                    if (next != '.' && !Char.IsNumber(next) && next != '-')
                    {
                        PutBack();
                        return scope;
                    }
                    PutBack();
                }
            }
            return scope;
        }
        public bool Get(ref char ch)
        {
            m_position++;
            if (m_position >= m_string.Length)
            {
                return false;
            }
            else
            {
                ch = m_string[m_position];
                return true;
            }
        }
        public void PutBack()
        {
            if (m_position > 0)
            {
                m_position--;
            }
        }
        internal bool EOF
        {
            get
            {
                return m_position >= m_string.Length;
            }
        }
        // properties
        private string m_string;
        private int m_position;
    }
}
