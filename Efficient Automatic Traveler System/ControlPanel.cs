using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Efficient_Automatic_Traveler_System
{
    abstract class Node
    {
        public Node() { }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>();
            obj.Add("type", this.GetType().Name.Quotate());
            return obj.Stringify();
        }
    }
    class TextNode : Node
    {
        public TextNode(string text, string color = "black", string textAlign = "center")
        {
            m_text = text;
            m_color = color;
            m_textAlign = textAlign;
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>();
            obj.Add("text", m_text.Quotate());
            obj.Add("color", m_color.Quotate());
            obj.Add("textAlign", m_textAlign.Quotate());
            return base.ToString().MergeJSON(obj.Stringify());
        }
        private string m_text;
        private string m_color;
        private string m_textAlign;
    }
    abstract class Control : Node
    {
        public Control(string name, string callback, string returnParam)
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
        public Button(string name, string callback, string returnParam = "{}") : base(name, callback, returnParam)
        {

        }
    }
    class Checkbox : Control
    {
        public Checkbox(string name, string callback, string returnParam = "{}") : base(name, callback, returnParam)
        {

        }
    }
    class Selection : Control
    {
        public Selection(string name, string callback, List<string> options, string value = "", string returnParam = "{}") : base (name, callback, returnParam) {
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

    abstract class PanelList : Node, IEnumerable<Node>
    {
        public PanelList()
        {
            m_nodes = new List<Node>();
        }
        public PanelList(Node[] elements)
        {
            m_nodes = elements.ToList();
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
    class Row : PanelList
    {
        public Row(string justify = "space-around", bool dividers = false) : base()
        {
            m_justify = justify;
            m_dividers = dividers;
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>();
            obj.Add("justify", m_justify.Quotate());
            obj.Add("dividers", m_dividers.ToString().ToLower());
            return base.ToString().MergeJSON(obj.Stringify());
        }
        private string m_justify;
        private bool m_dividers;
    }
    class Column : PanelList
    {
        public Column(string justify = "space-around", bool dividers = false) : base()
        {
            m_justify = justify;
            m_dividers = dividers;
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>();
            obj.Add("justify", m_justify.Quotate());
            obj.Add("dividers", m_dividers.ToString().ToLower());
            return base.ToString().MergeJSON(obj.Stringify());
        }
        private string m_justify;
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
