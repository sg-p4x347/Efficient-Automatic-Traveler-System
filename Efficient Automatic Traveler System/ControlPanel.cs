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
        public TextNode(string text)
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
        public Control(string name, string callback)
        {
            m_name = name;
            m_callback = callback;
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>();
            obj.Add("name", m_name.Quotate());
            obj.Add("callback", m_callback.Quotate());
            return base.ToString().MergeJSON(obj.Stringify());
        }
        private string m_name;
        private string m_callback;
    }
    class Button : Control
    {
        public Button(string name, string callback) : base(name, callback)
        {

        }
    }
    class Checkbox : Control
    {
        public Checkbox(string name, string callback) : base(name, callback)
        {

        }
    }
    class Selection : Control
    {
        public Selection(string name, string callback, List<string> options) : base (name, callback) {
            m_options = options;
        }
        public override string ToString()
        {
            Dictionary<string, string> obj = new Dictionary<string, string>();
            obj.Add("options", m_options.Stringify());
            return base.ToString().MergeJSON(obj.Stringify());
        }
        private List<string> m_options;
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
        public Row() : base() { }
        public Row(Node[] elements) : base(elements) { }
    }
    class Column : PanelList
    {
        public Column() : base() { }
        public Column(Node[] elements) : base(elements) { }
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
