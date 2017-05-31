using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    class Node
    {
        public Node() { }
        public Node(Dictionary<string, string> style = null, string DOMtype = "div")
        {
            m_style = style;
            m_DOMtype = DOMtype;
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>();
            obj.Add("type", this.GetType().Name.Quotate());
            obj.Add("DOMtype", m_DOMtype.Quotate());
            obj.Add("style", (m_style != null ? m_style.Stringify() : "{}"));
            return obj.Stringify();
        }
        private Dictionary<string, string> m_style;
        private string m_DOMtype;
    }
    class TextNode : Node
    {
        public TextNode(string text, Dictionary<string, string> style = null, string DOMtype = "p") : base(style,DOMtype)
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
    abstract class Control : Node
    {
        public Control(string name, string callback, string returnParam, Dictionary<string, string> style = null) : base(style)
        {
            m_name = name;
            m_callback = callback;
            m_returnParam = returnParam;
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>();
            obj.Add("name", m_name.Quotate());
            obj.Add("callback", m_callback.Quotate());
            obj.Add("returnParam", m_returnParam);
            return base.ToString().MergeJSON(obj.Stringify());
        }
        private string m_name;
        private string m_callback;
        private string m_returnParam;
    }
    class Button : Control
    {
        public Button(string name, string callback, string returnParam = "{}", Dictionary<string, string> style = null) : base(name, callback, returnParam, style)
        {

        }
    }
    class Checkbox : Control
    {
        public Checkbox(string name, string callback, string returnParam = "{}", Dictionary<string, string> style = null) : base(name, callback, returnParam, style)
        {
        }
    }
    class Selection : Control
    {
        public Selection(string name, string callback, List<string> options, string value = "", string returnParam = "{}", Dictionary<string, string> style = null) : base (name, callback, returnParam, style) {
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
    class RadioButtons : Selection
    {
        public RadioButtons(string name, string callback, List<string> options, string value = "", string returnParam = "{}", Dictionary<string, string> style = null) : base(name, callback, options, value, returnParam, style)
        {
        }
    }
    class NodeList : Node, IEnumerable<Node>
    {
        public NodeList(Dictionary<string, string> style = null, string DOMtype = "div") : base(style,DOMtype)
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
    class Row : NodeList
    {
        public Row(bool dividers = false, Dictionary<string, string> style = null) : base(style)
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
    class Column : NodeList
    {
        public Column(bool dividers = false, Dictionary<string, string> style = null) : base(style)
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
    class ControlPanel
    {
        public ControlPanel(string title, Node body)
        {
            m_title = title;
            m_body = body;
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>();
            obj.Add("title", m_title.Quotate());
            obj.Add("body", m_body.ToString());
            return obj.Stringify();
        }
        private string m_title;
        private Node m_body;
    }
}
