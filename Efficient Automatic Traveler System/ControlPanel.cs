using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;

namespace Efficient_Automatic_Traveler_System
{
    public class Style
    {
        public Style()
        {
            this.ClassNames = new List<string>();
            this.UniqueStyles = new Dictionary<string, string>();
        }
        public Style(params string[] classNames)
        {
            this.ClassNames = new List<string>(classNames);
            this.UniqueStyles = new Dictionary<string, string>();
        }
        public void AddStyle(string name, string style)
        {
            UniqueStyles.Add(name, style);
        }
        public static Style operator + (Style s1, Style s2)
        {
            Style style = new Style(s1.ClassNames.Concat(s2.ClassNames).ToArray());
            style.UniqueStyles = s1.UniqueStyles;
            style.UniqueStyles.Merge(s2.UniqueStyles);
            return style;
        }
        public List<string> ClassNames;
        public Dictionary<string,string> UniqueStyles;
    }
    public class Expand : Node
    {
        public Expand(Style style = null, string DOMtype = "div", string id = null) : base(style, DOMtype, id)
        {
        }
    }
    public class Node
    {
        public Node() {
            Style = new Style();
            m_innerHTML = "";
            Script = "";
        }
        public Node(Style style = null, string DOMtype = "div", string id = null)
        {
            m_style = (style != null ? style : new Style());
            m_DOMtype = DOMtype;
            ID = id;
            m_innerHTML = "";
            m_eventListeners = new List<EventListener>();
        }
        public override string ToString()
        {
            JsonObject obj = new JsonObject();
            obj.Add("type", this.GetType().Name);
            obj.Add("DOMtype", m_DOMtype);
            if (ID != null) obj.Add("id", ID);
            obj.Add("style", JsonObject.From(m_style.UniqueStyles));
            obj.Add("styleClasses", JsonArray.From(m_style.ClassNames));
            obj.Add("eventListeners", JsonArray.From(m_eventListeners));
            obj.Add("innerHTML", m_innerHTML);
            obj.Add("script", Script);
            return obj;
        }
        private Style m_style = new Style();
        private string m_DOMtype;
        private string m_id;
        private string m_innerHTML;
        private string m_script = "";
        private List<EventListener> m_eventListeners;

        public Style Style
        {
            get
            {
                return m_style;
            }

            set
            {
                m_style = value;
            }
        }

        public List<EventListener> EventListeners
        {
            get
            {
                return m_eventListeners;
            }

            set
            {
                m_eventListeners = value;
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

        public string InnerHTML
        {
            get
            {
                return m_innerHTML;
            }

            set
            {
                m_innerHTML = value;
            }
        }

        public string Script
        {
            get
            {
                return m_script;
            }

            set
            {
                m_script = value;
            }
        }

        // Specialized Nodes
        public static Node Img(Style style = null)
        {
            return new Node(style: style, DOMtype: "img");
        }
    }
    public class TextNode : Node
    {
        public TextNode(string text,Style style = null, string DOMtype = "p") : base(style,DOMtype)
        {
            m_text = text != null ? text : "";
        }
        public override string ToString()
        {
            JsonObject obj = (JsonObject)JSON.Parse(base.ToString());
            obj.Add("text", m_text);
            return obj;
        }
        private string m_text;
    }
    public abstract class Control : Node
    {
        public Control(string type, string name, string callback, string returnParam,Style style = null) : base(style)
        {
            m_name = name;
            EventListeners.Add(new EventListener(type, callback, returnParam));
        }
        public override string ToString()
        {
            JsonObject obj = (JsonObject)JSON.Parse(base.ToString());
            obj.Add("name", m_name);
            return obj;
        }
        private string m_name;
    }
    public class Button : Control
    {
        public Button(string name, string callback, string returnParam = "{}", Style style = null) : base("click", name, callback, returnParam, style)
        {
            
        }
    }
    public class Checkbox : Control
    {
        public Checkbox(string name, string callback, string returnParam = "{}", bool value = false , Style style = null) : base("change", name, callback, returnParam, style)
        {
            m_value = value; 
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>();
            obj.Add("value", m_value.ToString().ToLower());
            return base.ToString().MergeJSON(obj.Stringify());
        }
        private bool m_value = false;
    }
    public class Selection : Control
    {
        public Selection(string name, string callback, List<string> options, string value = "", string returnParam = "{}", Style style = null) : base ("change",name, callback, returnParam, style) {
            m_options = options;
            m_value = value;
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>();
            obj.Add("options", m_options.Stringify());
            obj.Add("value", m_value.Quotate());
            return base.ToString().MergeJSON(obj.Stringify());
        }
        private List<string> m_options;
        private string m_value;
    }
    public class RadioButtons : Selection
    {
        public RadioButtons(string name, string callback, List<string> options, string value = "", string returnParam = "{}",Style style = null) : base(name, callback, options, value, returnParam, style)
        {
        }
    }
    public class NodeList : Node, IEnumerable<Node>
    {
        public NodeList(Style style = null, string DOMtype = "div") : base(style, DOMtype)
        {
            m_nodes = new List<Node>();
        }
        public void Add(Node node)
        {
            m_nodes.Add(node);
        }
        public override string ToString()
        {
            JsonObject obj = (JsonObject)JSON.Parse(base.ToString());
            obj.Add("nodes", JsonArray.From(m_nodes));
            return obj;
        }
        public List<Node> Nodes
        {
            get { return m_nodes; }
        }
        // IEnumerable<Node>
        public Node this[int index]
        {
            get { return m_nodes[index]; }
            set { m_nodes.Insert(index, value); }
        }

        public IEnumerator<Node> GetEnumerator()
        {
            return m_nodes.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
        private List<Node> m_nodes;
    }
    public class Row : NodeList
    {
        public Row(bool dividers = false, Style style = null) : base(style)
        {
            m_dividers = dividers;
        }
        public override string ToString()
        {
            JsonObject obj = (JsonObject)JSON.Parse(base.ToString());
            obj.Add("dividers", m_dividers);
            return obj;
        }
        private bool m_dividers;
    }
    public class Column : NodeList
    {
        public Column(bool dividers = false, Style style = null) : base(style)
        {
            m_dividers = dividers;
        }
        public override string ToString()
        {
            JsonObject obj = (JsonObject)JSON.Parse(base.ToString());
            obj.Add("dividers", m_dividers);
            return obj;
        }
        private bool m_dividers;
    }
    public class ControlPanel
    {
        public ControlPanel(string title, Node body, string id = "")
        {
            m_title = title;
            m_body = body;
            m_ID = id;
        }
        public override string ToString()
        {
            JsonObject obj = new JsonObject() {
                { "title", m_title },
                { "body", m_body },
                { "ID", m_ID}
            };
            return obj;
        }
        public ClientMessage Dispatch(bool closeAll = true)
        {
            JsonObject obj = (JsonObject)JSON.Parse(ToString());
            obj.Add("closeAll", closeAll);
            return new ClientMessage("ControlPanel", obj);
        }
        private string m_title;
        private Node m_body;
        private string m_ID;

        // Static html creation helpers
        public static Node CreateDataTable(DataTable dataTable)
        {
            Style cellStyle = new Style("mediumBorder");
            NodeList table = new NodeList(cellStyle,"table");
            // create the header
            NodeList header = new NodeList(DOMtype: "tr");
            foreach (DataColumn column in dataTable.Columns)
            {
                header.Add(new TextNode(column.ColumnName, cellStyle, "th"));
            }
            table.Add(header);
            // create the detail
            foreach (DataRow row in dataTable.Rows)
            {
                NodeList detail = new NodeList(DOMtype: "tr");
                foreach(DataColumn column in dataTable.Columns)
                {
                    JSON json = JSON.Parse(row[column].ToString());
                    if (typeof(Node).IsAssignableFrom(column.DataType))
                    {
                        NodeList td = new NodeList(cellStyle, "td");
                        td.Add(row[column] as Node);
                        detail.Add(td);
                    }
                    else
                    {
                        detail.Add(new TextNode(row[column].ToString(), cellStyle, "td"));
                    }
                }
                table.Add(detail);
            }
            return table;
        }
        public static Node CreateDictionary(Dictionary<string, Node> dictionary)
        {
            //=================================
            // STYLES
            Style spaceBetween = new Style("justify-space-between");
            Style leftAlign = new Style("leftAlign");
            Style rightAlign = new Style("rightAlign");
            Style shadow = new Style("shadow");
            Style white = new Style("white");
            //=================================

            Column list = new Column(true);
            foreach (KeyValuePair<string, Node> pair in dictionary)
            {
                Row row = new Row(style: spaceBetween);
                // key
                row.Add(new TextNode(pair.Key, leftAlign));
                // value
                row.Add(pair.Value as Node);

                list.Add(row);
            }
            return list;
        }
        public static Node CreateList(List<object> list)
        {
            Column column = new Column();
            foreach (object item in list)
            {
                if (item is string)
                {
                    column.Add(new TextNode(item as string));
                } else if (item is Node)
                {
                    column.Add(item as Node);
                }
            }
            return column;
        }
        public static Node FormattedText(string text, Style style = null)
        {
            List<object> lines = new List<object>();
            foreach ( string line in text.Split('\n').ToList())
            {
                lines.Add(line);
            }
            NodeList nodes = (NodeList)CreateList(lines);
            if (style != null)
            {
                foreach (Node node in nodes)
                {
                    node.Style += style;
                }
            }
            return nodes;
        }

        // Popups
        public static ClientMessage YesOrNo(string text, string yesCallback = "CloseAll", string noCallback = "CloseAll", string returnParam = "{}")
        {
            Column column = new Column()
            {
                new TextNode(text),
                new Row(style: new Style("justify-space-around"))
                {
                    new Button("Yes",yesCallback,returnParam),
                    new Button("No",noCallback,returnParam)
                }
            };
            return new ControlPanel("", column).Dispatch();
        }
        public static NodeList Options(string text, Dictionary<string,string> options, string returnParam = "{}")
        {
            Column column = new Column()
            {
                new TextNode(text)
            };
            Row row = new Row();
            foreach (KeyValuePair<string,string> option in options)
            {
                row.Add(new Button(option.Key, option.Value, returnParam));
            }
            column.Add(row);
            return column;
        }
        public static Node PrintForm(Form form)
        {
            Dictionary<string, Node> list = new Dictionary<string, Node>();
            foreach (JsonObject field in (JsonArray)form.ToJSON()["fields"])
            {
                list.Add(field["title"], new TextNode(field["value"], new Style("white","shadow")));
            }
            return ControlPanel.CreateDictionary(list);
        }
        public static Node FormatJSON(JSON obj)
        {
            Dictionary<string, Node> list = new Dictionary<string, Node>();
            if (obj is JsonObject)
            {
                foreach (string key in ((JsonObject)obj).Keys)
                {
                    list.Add(key, FormatJSON(obj[key]));
                }
            } else if (obj is JsonArray)
            {
                int index = 0;
                foreach (JSON sub in ((JsonArray)obj))
                {
                    list.Add(index.ToString(), FormatJSON(sub));
                    index++;
                }
            } else
            {
                return new TextNode(obj);
            }
            return ControlPanel.CreateDictionary(list);
        }

        // Edit HTML element by ID
        public static ClientMessage EditHTML(string id, string method, string parameters)
        {
            return new ClientMessage("EditHTML", new JsonObject() { { "id", id }, { "method", method }, { "params", parameters } });
        }
        public static ClientMessage AddStyle(string id, Style style)
        {
            if (style.ClassNames.Any())
            {
                return new ClientMessage("AddStyle", new JsonObject() { { "id", id }, { "style", style.ClassNames.First() } });
            }
            return new ClientMessage();
        }
        public static ClientMessage RemoveStyle(string id, Style style)
        {
            if (style.ClassNames.Any())
            {
                return new ClientMessage("RemoveStyle", new JsonObject() { { "id", id }, { "style", style.ClassNames.First() } });
            }
            return new ClientMessage();
        }
    }

    public class EventListener
    {
        public EventListener(string type, string callback, string returnParam)
        {
            m_type = type;
            m_callback = callback;
            m_returnParam = returnParam;
        }
        public override string ToString()
        {
            return new JsonObject() {
                { "type", m_type },
                { "callback", m_callback },
                { "returnParam", JSON.Parse(m_returnParam )}
            };
        }
        private string m_type;
        private string m_callback;
        private string m_returnParam;
    }
    
}
