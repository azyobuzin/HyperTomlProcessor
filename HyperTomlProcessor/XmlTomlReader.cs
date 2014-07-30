using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace HyperTomlProcessor
{
    public class XmlTomlReader : XmlDictionaryReader
    {
        public XmlTomlReader(TomlReader reader)
        {
            this.reader = reader;
        }

        public XmlTomlReader(TextReader reader)
            : this(new TomlReader(reader)) { }

        private readonly TomlReader reader;
        private bool initial = true;
        private bool eof;
        private bool closed;

        private XNode node;
        private XAttribute attr;
        private bool isAttr;
        private bool isAttrValue;
        private bool isEnd;

        private bool alreadyRead;
        private bool isNextEOF;
        private bool isNextEndElement;
        private int arrayDim = 0;
        private bool isNextValue;
        private XElement nextArrayOfTableItem;

        private XElement root = new XElement("root", new XAttribute("type", "object"), new XAttribute("toml", "table"));

        private static readonly Dictionary<TomlNodeType, string> tomlType = new Dictionary<TomlNodeType, string>()
        {
            {TomlNodeType.BasicString, "basicString"},
            {TomlNodeType.MultilineBasicString, "multi-lineBasicString"},
            {TomlNodeType.LiteralString, "literalString"},
            {TomlNodeType.MultilineLiteralString, "multi-lineLiteralString"},
            {TomlNodeType.Integer, "integer"},
            {TomlNodeType.Float, "float"},
            {TomlNodeType.Boolean, "boolean"},
            {TomlNodeType.Datetime, "datetime"},
            {TomlNodeType.StartArray, "array"},
            {TomlNodeType.StartTable, "table"},
            {TomlNodeType.StartArrayOfTable, "arrayOfTable"}
        };

        private struct TableName
        {
            public string Name;
            public bool IsArrayOfTable;

            public TableName(string name, bool isArrayOfTable)
            {
                this.Name = name;
                this.IsArrayOfTable = isArrayOfTable;
            }
        }
        private readonly LinkedList<TableName> position = new LinkedList<TableName>();
        private bool isTableContent;

        private static readonly bool[] ValidFirstName;
        private static readonly bool[] ValidName;
        private static void Allow(bool[] a, int start, int end)
        {
            for (var i = start; i <= end; i++)
                a[i] = true;
        }
        static XmlTomlReader()
        {
            ValidFirstName = new bool[256];
            Allow(ValidFirstName, 0x41, 0x5A);
            ValidFirstName[0x5F] = true;
            Allow(ValidFirstName, 0x61, 0x7A);
            Allow(ValidFirstName, 0x80, 0xFF);

            ValidName = new bool[256];
            Allow(ValidName, 0x2D, 0x2E);
            Allow(ValidName, 0x30, 0x39);
            Allow(ValidName, 0x41, 0x5A);
            ValidName[0x5F] = true;
            Allow(ValidName, 0x61, 0x7A);
        }
        private static bool IsValidName(string name)
        {
            var bytes = Encoding.UTF8.GetBytes(name);
            if (!ValidFirstName[bytes[0]]) return false;
            foreach (var b in bytes)
            {
                if (!ValidName[b]) return false;
            }
            return true;
        }

        private static readonly XNamespace namespaceA = "item";
        private static XAttribute prefixA
        {
            get
            {
                return new XAttribute(XNamespace.Xmlns + "a", namespaceA);
            }
        }

        private T DoOnElement<T>(Func<XElement, T> action, T def = default(T))
        {
            var elm = this.node as XElement;
            if (elm == null) return def;
            return action(elm);
        }

        public override int AttributeCount
        {
            get
            {
                return this.DoOnElement(elm => elm.Attributes().Count());
            }
        }

        public override string BaseURI
        {
            get { return string.Empty; }
        }

        public override void Close()
        {
            this.reader.Dispose();
            this.closed = true;
        }

        public override int Depth
        {
            get
            {
                if (this.node == null) return 0;
                var i = 0;
                var n = this.node.Parent;
                while (n != null)
                {
                    i++;
                    n = n.Parent;
                }
                if (this.isAttrValue) i++;
                return i;
            }
        }

        public override bool EOF
        {
            get { return this.eof; }
        }

        public override string GetAttribute(int i)
        {
            return this.DoOnElement(elm =>
            {
                var attr = elm.FirstAttribute;
                return attr != null ? attr.Value : null;
            });
        }

        public override string GetAttribute(string name, string namespaceURI)
        {
            return this.DoOnElement(elm =>
            {
                var attr = elm.Attribute(XName.Get(name, namespaceURI));
                return attr != null ? attr.Value : null;
            });
        }

        public override string GetAttribute(string name)
        {
            return this.DoOnElement(elm =>
            {
                var attr = elm.Attribute(name);
                return attr != null ? attr.Value : null;
            });
        }

        public override bool IsEmptyElement
        {
            get
            {
                return false;
            }
        }

        public override string LocalName
        {
            get
            {
                return this.NameTable.Add(
                    this.isAttr
                        ? this.attr.Name.LocalName
                        : this.DoOnElement(elm => elm.Name.LocalName, "")
                );
            }
        }

        public override string LookupNamespace(string prefix)
        {
            return null;
        }

        private bool MoveToAttribute(XAttribute xa)
        {
            if (xa == null) return false;
            this.attr = xa;
            this.isAttr = true;
            this.isAttrValue = false;
            return true;
        }

        public override bool MoveToAttribute(string name, string ns)
        {
            return this.DoOnElement(elm => this.MoveToAttribute(elm.Attribute(XName.Get(name, ns))));
        }

        public override bool MoveToAttribute(string name)
        {
            return this.DoOnElement(elm => this.MoveToAttribute(elm.Attribute(name)));
        }

        public override bool MoveToElement()
        {
            if (!(this.isAttr || this.isAttrValue)) return false;
            this.isAttr = false;
            this.isAttrValue = false;
            return true;
        }

        public override bool MoveToFirstAttribute()
        {
            return this.DoOnElement(elm => this.MoveToAttribute(elm.FirstAttribute));
        }

        public override bool MoveToNextAttribute()
        {
            return this.isAttr
                ? this.MoveToAttribute(this.attr.NextAttribute)
                : this.MoveToFirstAttribute();
        }

        private XmlNameTable nameTable = new NameTable();
        public override XmlNameTable NameTable
        {
            get { return this.nameTable; }
        }

        public override string NamespaceURI
        {
            get
            {
                return this.isAttr
                    ? this.attr.Name.NamespaceName
                    : this.DoOnElement(elm => elm.Name.NamespaceName, "");
            }
        }

        public override XmlNodeType NodeType
        {
            get
            {
                return this.initial ? XmlNodeType.None
                    : this.isAttr ? XmlNodeType.Attribute
                    : this.isAttrValue ? XmlNodeType.Text
                    : node is XElement ? (this.isEnd ? XmlNodeType.EndElement : XmlNodeType.Element)
                    : node is XText ? XmlNodeType.Text
                    : node is XComment ? XmlNodeType.Comment
                    : XmlNodeType.None;
            }
        }

        public override string Prefix
        {
            get
            {
                return this.DoOnElement(elm => elm.GetPrefixOfNamespace(XNamespace.Get(this.NamespaceURI)) ?? "");
            }
        }

        private string GetTypeString()
        {
            switch (this.reader.NodeType)
            {
                case TomlNodeType.BasicString:
                case TomlNodeType.MultilineBasicString:
                case TomlNodeType.LiteralString:
                case TomlNodeType.MultilineLiteralString:
                case TomlNodeType.Datetime:
                    return "string";
                case TomlNodeType.Integer:
                case TomlNodeType.Float:
                    return "number";
                case TomlNodeType.Boolean:
                    return "boolean";
                case TomlNodeType.StartArray:
                    return "array";
                default:
                    throw new XmlException("Invalid break.", null, this.reader.LineNumber, this.reader.LinePosition);
            }
        }

        private void EndThisTable()
        {
            this.node = this.isTableContent ? this.node.Parent : this.node;
            this.isEnd = true;
            this.isNextEndElement = this.position.Last.Value.IsArrayOfTable; // <item> も閉じる
            this.position.RemoveLast();
        }

        private static XElement CreateKeyElement(string key, string type, string tomlType)
        {
            return IsValidName(key)
                ? new XElement(key,
                    new XAttribute("type", type),
                    new XAttribute("toml", tomlType)
                )
                : new XElement(namespaceA + "item",
                    prefixA,
                    new XAttribute("item", key),
                    new XAttribute("type", type),
                    new XAttribute("toml", tomlType)
                );
        }

        public override bool Read()
        {
            if (this.initial)
            {
                this.initial = false;
                this.node = this.root;
                return true;
            }

            if (this.isNextEOF)
            {
                this.isNextEOF = false;
                this.eof = true;
                return false;
            }

            this.isAttr = false;
            this.isAttrValue = false;
            this.isEnd = false;

            if (this.isNextEndElement)
            {
                this.isNextEndElement = false;
                this.node = this.node.Parent;
                this.isEnd = true;
                return true;
            }

            if (this.nextArrayOfTableItem != null)
            {
                this.node = this.nextArrayOfTableItem;
                this.nextArrayOfTableItem = null;
                return true;
            }

            var xe = this.node as XElement;

            if (this.isNextValue)
            {
                this.isNextValue = false;
                this.node = xe.FirstNode;
                this.isNextEndElement = true;
                return true;
            }

            if (this.alreadyRead) this.alreadyRead = false;
            else
            {
                try
                {
                    do
                    {
                        this.isNextEOF = !this.reader.Read();
                        if (this.isNextEOF)
                        {
                            this.node = root;
                            this.isEnd = true;
                            return true;
                        }
                    } while (this.reader.NodeType == TomlNodeType.EndLine);
                }
                catch (TomlException ex)
                {
                    throw new XmlException("An exception has been thrown.", ex, ex.LineNumber, ex.LinePosition);
                }
            }

            switch (this.reader.NodeType)
            {
                case TomlNodeType.Comment:
                    if (this.isTableContent)
                        xe = this.node.Parent;
                    xe.AddFirst(new XComment((string)this.reader.Value));
                    this.node = xe.FirstNode;
                    this.isTableContent = true;
                    break;
                case TomlNodeType.Key:
                    var key = (string)this.reader.Value;
                    var f = this.reader.Read();
                    if (!f) throw new XmlException("EOF", null, this.reader.LineNumber, this.reader.LinePosition);
                    this.alreadyRead = true;
                    if (this.reader.NodeType == TomlNodeType.StartArray)
                    {
                        this.arrayDim++;
                        this.alreadyRead = false;
                    }
                    if (this.isTableContent)
                        xe = (XElement)this.node.Parent;
                    xe.RemoveNodes();
                    xe.AddFirst(CreateKeyElement(key, this.GetTypeString(), tomlType[this.reader.NodeType]));
                    this.node = xe.FirstNode;
                    this.isTableContent = true;
                    break;
                case TomlNodeType.BasicString:
                case TomlNodeType.MultilineBasicString:
                case TomlNodeType.LiteralString:
                case TomlNodeType.MultilineLiteralString:
                case TomlNodeType.Integer:
                case TomlNodeType.Float:
                case TomlNodeType.Datetime:
                case TomlNodeType.Boolean:
                    var s = this.reader.Value.ToString();
                    if (this.reader.NodeType == TomlNodeType.Boolean)
                        s = s.ToLowerInvariant();
                    if (this.arrayDim > 0)
                    {
                        if (xe.Attribute("toml").Value != "array")
                            xe = xe.Parent;
                        xe.RemoveNodes();
                        xe.AddFirst(new XElement("item",
                            new XAttribute("type", this.GetTypeString()),
                            new XAttribute("toml", tomlType[this.reader.NodeType]),
                            new XText(s)
                        ));
                        this.node = xe.FirstNode;
                        this.isNextValue = true;
                    }
                    else
                    {
                        xe.AddFirst(new XText(s));
                        this.node = xe.FirstNode;
                        this.isNextEndElement = true;
                    }
                    break;
                case TomlNodeType.StartArray:
                    if (this.arrayDim > 0)
                    {
                        this.arrayDim++;
                        xe.RemoveNodes();
                        xe.AddFirst(new XElement("item",
                            new XAttribute("type", "array"),
                            new XAttribute("toml", "array")
                        ));
                        this.node = xe.FirstNode;
                    }
                    else
                    {
                        throw new XmlException("Array is not allowed here.", null, this.reader.LineNumber, this.reader.LinePosition);
                    }
                    break;
                case TomlNodeType.EndArray:
                    this.arrayDim--;
                    this.node = this.node.Parent;
                    this.isEnd = true;
                    break;
                case TomlNodeType.StartTable:
                    {
                        var name = ((string)this.reader.Value).Split('.');
                        var matched = this.position.Zip(name, (x, y) => x.Name == y).TakeWhile(_ => _).Count();
                        if (matched == name.Length)
                        {
                            throw new XmlException("Invalid the name of the table.", null, this.reader.LineNumber, this.reader.LinePosition);
                        }
                        else if (this.position.Count == matched)
                        {
                            if (this.isTableContent)
                                xe = this.node.Parent;
                            xe.RemoveNodes();
                            xe.AddFirst(CreateKeyElement(name[matched], "object", "table"));
                            this.node = xe.FirstNode;
                            this.position.AddLast(new TableName(name[matched], false));
                            this.isTableContent = false;
                            this.alreadyRead = name.Length > matched + 1;
                        }
                        else
                        {
                            this.EndThisTable();
                            this.alreadyRead = true;
                        }
                    }
                    break;
                case TomlNodeType.StartArrayOfTable:
                    {
                        var name = ((string)this.reader.Value).Split('.');
                        var matched = this.position.Zip(name, (x, y) => x.Name == y).TakeWhile(_ => _).Count();
                        if (name.Length == matched)
                        {
                            // array の次の要素
                            if (this.isTableContent)
                                this.node = this.node.Parent;
                            this.isEnd = true;

                            xe = (XElement)this.node;
                            xe.Add(new XElement("item",
                                new XAttribute("type", "object"),
                                new XAttribute("toml", "table")
                            ));
                            this.nextArrayOfTableItem = (XElement)xe.LastNode;
                        }
                        else if (name.Length == matched + 1)
                        {
                            xe = this.node.Parent;
                            xe.RemoveNodes();
                            xe.AddFirst(new XElement(name[name.Length - 1],
                                new XAttribute("type", "array"),
                                new XAttribute("toml", "arrayOfTable")
                            ));
                            this.node = xe.FirstNode;
                            this.position.AddLast(new TableName(name[name.Length - 1], true));

                            xe = (XElement)this.node;
                            xe.AddFirst(new XElement("item",
                                new XAttribute("type", "object"),
                                new XAttribute("toml", "table")
                            ));
                            this.nextArrayOfTableItem = (XElement)xe.FirstNode;
                        }
                        else
                        {
                            throw new XmlException("Invalid the name of the array of table.", null, this.reader.LineNumber, this.reader.LinePosition);
                        }
                        this.isTableContent = false;
                    }
                    break;
            }

            return true;
        }

        public override bool ReadAttributeValue()
        {
            if (this.isAttr)
            {
                this.isAttr = false;
                this.isAttrValue = true;
                return true;
            }
            return false;
        }

        public override ReadState ReadState
        {
            get
            {
                return this.initial ? ReadState.Initial
                    : this.closed ? ReadState.Closed
                    : this.eof ? ReadState.EndOfFile
                    : ReadState.Interactive;
            }
        }

        public override void ResolveEntity()
        {
            throw new InvalidOperationException();
        }

        public override string Value
        {
            get
            {
                if (this.isAttr || this.isAttrValue)
                    return this.attr.Value;

                var xt = this.node as XText;
                if (xt != null)
                    return xt.Value;

                var xc = this.node as XComment;
                if (xc != null)
                    return xc.Value;

                return "";
            }
        }
    }
}
