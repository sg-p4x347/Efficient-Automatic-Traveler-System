using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    public class Form
    {
        #region Public Methods
        public Form()
        {
            m_title = "";
            m_source = "";
            m_fields = new List<string>();
            ID = "";
        }
        public Form(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                if (obj.ContainsKey("name"))
                {
                    m_title = obj["name"];
                    //m_source = obj["source"];
                    m_fields = new StringStream(obj["fields"]).ParseJSONarray();
                } else
                {
                    JsonObject obj2 = (JsonObject)JSON.Parse(json);
                    m_title = ((JsonObject)obj2["form"])["name"];
                    m_fields = new StringStream(((JsonArray)((JsonObject)obj2["form"])["fields"])).ParseJSONarray();
                }
            } catch (Exception ex)
            {
                Server.LogException(ex);
            }
        }
        public virtual Dictionary<string, Node> ExportViewProperties()
        {
            Dictionary<string, Node> list = new Dictionary<string, Node>();
            foreach (string json in m_fields)
            {
                JsonObject field = (JsonObject)JSON.Parse(json);
                list.Add(field["title"], new TextNode(field["value"]));
            }
            return list;
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>() {
                {"name",m_title.Quotate() },
                {"fields",m_fields.Stringify(false) },
                {"id",ID.Quotate() }
            };
            return obj.Stringify();
        }
        public JSON ToJSON()
        {
            return JSON.Parse(ToString());
        }
        public ClientMessage Dispatch(string callback, string parameters = "{}")
        {
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"form",ToString() },
                {"callback",callback.Quotate() },
                {"parameters",parameters }
            };
            return new ClientMessage("Form", obj.Stringify());
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
        public void Textarea(string name, string title, string value = "")
        {
            m_fields.Add(Basic(name, title, "textarea", value.Quotate()));
        }
        public void Integer(string name, string title, int min = int.MinValue, int max = int.MaxValue, int value = 0)
        {
            string basic = Basic(name, title, "number", value.ToString());
            Dictionary<string, string> obj = new Dictionary<string, string>()
            {
                {"min",min.ToString() },
                {"max",max.ToString() }
            };
            m_fields.Add(basic.MergeJSON(obj.Stringify()));
        }
        public void Checkbox(string name,string title, bool value = false)
        {
            m_fields.Add(Basic(name,title, "checkbox",value.ToString().ToLower()));
        }
        public void Selection<T>(string name, string title, string value = "")
        {
            Selection(name,title, ExtensionMethods.GetNames<T>());
        }
        public void Selection(string name, string title, List<string> options, string value = "", string type = "select")
        {
            Dictionary<string, string> obj = new Dictionary<string, string>() {
                {"type",type.Quotate() },
                {"name",name.Quotate() },
                {"title",title.Quotate() },
                {"options",options.Stringify() },
                {"value",value.Quotate() }
            };
            m_fields.Add(obj.Stringify());
        }
        public void AddBox(string name, string title)
        {
            JsonObject obj = new JsonObject()
            {
                {"type","addBox" },
                {"name",name },
                {"title",title }
            };
            m_fields.Add(obj);
        }
        public void Radio(string name, string title, List<string> options, string value = "")
        {
            Selection(name, title, options, value, "radio");
        }
        public void Date(string name, string title, string value = "")
        {
            m_fields.Add(Basic(name, title, "date", value.Quotate()));
        }
        #endregion

        #region Static helpers
        public static Form DateRange()
        {
            Form form = new Form();
            form.Date("from", "From");
            form.Date("to", "To");
            return form;
        }
        public static void DateRange(Form form, out DateTime from, out DateTime to)
        {
            DateTime.TryParse(form.ValueOf("from"), out from);
            DateTime.TryParse(form.ValueOf("to"), out to);
        }
        public static Form CommentForm(string title, string fieldName, string fieldTitle = null)
        {
            Form form = new Form();
            form.Title = title;
            form.Textbox(fieldName, fieldTitle != null ? fieldTitle : fieldName);
            return form;
        }
        #endregion
        //---------------------------------------------------------

        #region Private Methods

        // basic form element
        private string Basic(string name, string title, string type, string value)
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
        private string m_title;
        private List<string> m_fields;
        private string m_source;
        private string m_id;
        public string Name
        {
            get
            {
                return m_title;
            }

            set
            {
                m_title = value;
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

        public string Title
        {
            get
            {
                return m_title;
            }

            set
            {
                m_title = value;
            }
        }

        public List<string> Fields
        {
            get
            {
                return m_fields;
            }
        }

        public string ID
        {
            get
            {
                return m_id;
            }

            set
            {
                m_id = value;
            }
        }

        #endregion
    }
}
