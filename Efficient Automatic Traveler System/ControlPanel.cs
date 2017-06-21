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
            UniqueStyles.Add(name, style.Quotate());
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
    public class Node
    {
        public Node() {
            Style = new Style();
        }
        public Node(Style style = null, string DOMtype = "div")
        {
            m_style = (style != null ? style : new Style());
            m_DOMtype = DOMtype;
            m_eventListeners = new List<EventListener>();
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>();
            obj.Add("type", this.GetType().Name.Quotate());
            obj.Add("DOMtype", m_DOMtype.Quotate());
            obj.Add("style", m_style.UniqueStyles.Stringify());
            obj.Add("styleClasses", m_style.ClassNames.Stringify());
            obj.Add("eventListeners", m_eventListeners.Stringify());
            return obj.Stringify();
        }
        private Style m_style = new Style();
        private string m_DOMtype;
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
            m_text = text;
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>();
            obj.Add("text", m_text.Quotate());
            return base.ToString().MergeJSON(obj.Stringify());
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
            Dictionary<string, string> obj = new Dictionary<string, string>();
            obj.Add("name", m_name.Quotate());
            return base.ToString().MergeJSON(obj.Stringify());
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
        public Checkbox(string name, string callback, string returnParam = "{}", Style style = null) : base("change", name, callback, returnParam, style)
        {
        }
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
            List<string> strings = new List<string>();
            foreach (Node node in m_nodes)
            {
                strings.Add(node.ToString());
            }
            Dictionary<string, string> obj = new Dictionary<string, string>();
            obj.Add("nodes", strings.Stringify(false));
            return base.ToString().MergeJSON(obj.Stringify());
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
            Dictionary<string, string> obj = new Dictionary<string, string>();
            obj.Add("dividers", m_dividers.ToString().ToLower());
            return base.ToString().MergeJSON(obj.Stringify());
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
            Dictionary<string, string> obj = new Dictionary<string, string>();
            obj.Add("dividers", m_dividers.ToString().ToLower());
            return base.ToString().MergeJSON(obj.Stringify());
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
            Dictionary<string, string> obj = new Dictionary<string, string>();
            obj.Add("title", m_title.Quotate());
            obj.Add("body", m_body.ToString());
            obj.Add("ID", m_ID.Quotate());
            return obj.Stringify();
        }
        public ClientMessage Dispatch()
        {
            return new ClientMessage("ControlPanel", ToString());
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
            // create the detail
            foreach (DataRow row in dataTable.Rows)
            {
                NodeList detail = new NodeList(DOMtype: "tr");
                foreach(DataColumn column in dataTable.Columns)
                {
                    detail.Add(new TextNode(row[column].ToString(), cellStyle, "td"));
                }
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
            Dictionary<string, string> obj = new Dictionary<string, string>();
            obj.Add("type", m_type.Quotate());
            obj.Add("callback", m_callback.Quotate());
            obj.Add("returnParam", m_returnParam);
            return obj.Stringify();
        }
        private string m_type;
        private string m_callback;
        private string m_returnParam;
    }
    
}
