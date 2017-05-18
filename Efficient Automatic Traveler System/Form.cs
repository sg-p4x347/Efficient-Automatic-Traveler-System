﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    public class Form
    {
        #region Public Methods
        public Form(Type type)
        {
            m_name = type.Name;
            m_source = "";
            m_fields = new List<string>();
        }
        public Form(string json)
        {
            Dictionary<string, string> obj = new StringStream(json).ParseJSON();
            m_name = obj["name"];
            //m_source = obj["source"];
            m_fields = new StringStream(obj["fields"]).ParseJSONarray();
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>() {
                {"name",m_name.Quotate() },
                {"fields",m_fields.Stringify(false) }
            };
            return obj.Stringify();
        }
        public string ValueOf(string fieldName)
        {
            foreach (string field in m_fields)
            {
                Dictionary<string, string> obj = new StringStream(field).ParseJSON();
                if (obj["name"] == fieldName)
                {
                    return obj["value"];
                }
            }
            return "";
        }
        //=======================================
        // Form Elements
        //=======================================
        
        public void Textbox(string name, string title, string value = "")
        {
            m_fields.Add(Basic(name,title, "text",value.Quotate()));
        }
        
        public void Integer(string name, string title, int value = 0)
        {
            m_fields.Add(Basic(name,title, "number",value.ToString()));
        }
        public void Checkbox(string name,string title, bool value = false)
        {
            m_fields.Add(Basic(name,title, "checkbox",value.ToString().ToLower()));
        }
        public void Selection<T>(string name, string title, string value = "")
        {
            Selection(name,title, ExtensionMethods.GetNames<T>());
        }
        public void Selection(string name, string title, List<string> options, string value = "")
        {
            Dictionary<string, string> obj = new Dictionary<string, string>() {
                {"type","select".Quotate() },
                {"name",name.Quotate() },
                {"title",title.Quotate() },
                {"options",options.Stringify() },
                {"value",value.Quotate() }
            };
            m_fields.Add(obj.Stringify());
        }
        #endregion
        //---------------------------------------------------------

        #region Private Methods

        // basic form element
        private static string Basic(string name, string title, string type, string value)
        {
            Dictionary<string, string> obj = new Dictionary<string, string>() {
                {"type",type.Quotate() },
                {"name",name.Quotate() },
                {"title",title.Quotate() },
                {"value",value }
            };
            return obj.Stringify();
        }
        #endregion

        //---------------------------------------------------------

        #region Properties
        private string m_name;
        private List<string> m_fields;
        private string m_source;
        public string Name
        {
            get
            {
                return m_name;
            }

            set
            {
                m_name = value;
            }
        }

        public string Source
        {
            get
            {
                return m_source;
            }

            set
            {
                m_source = value;
            }
        }
        #endregion
    }
}