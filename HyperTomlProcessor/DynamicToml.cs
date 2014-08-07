using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Xml.Linq;

namespace HyperTomlProcessor
{
    /// <summary>
    /// Represents a TOML table or array.
    /// </summary>
    public class DynamicToml : DynamicObject, IEnumerable<object>
    {
        private static object ToValue(XElement xe)
        {
            switch (XUtils.GetTomlAttr(xe))
            {
                case TomlItemType.BasicString:
                case TomlItemType.MultilineBasicString:
                case TomlItemType.LiteralString:
                case TomlItemType.MultilineLiteralString:
                    return xe.Value;
                case TomlItemType.Integer:
                    return (long)xe;
                case TomlItemType.Float:
                    return (double)xe;
                case TomlItemType.Boolean:
                    return (bool)xe;
                case TomlItemType.Datetime:
                    return (DateTimeOffset)xe;
                default:
                    return new DynamicToml(xe);
            }
        }

        private static TomlItemType GetTomlType(object obj)
        {

            if (obj == null) throw new ArgumentNullException("obj");

            switch (Type.GetTypeCode(obj.GetType()))
            {
                case TypeCode.Boolean:
                    return TomlItemType.Boolean;
                case TypeCode.String:
                case TypeCode.Char:
                    return TomlItemType.BasicString;
                case TypeCode.DateTime:
                    return TomlItemType.Datetime;
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return TomlItemType.Float;
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return TomlItemType.Integer;
                default:
                    //TODO: Dictionary に対応
                    var dt = obj as DynamicToml;
                    return dt != null
                        ? (dt.isArray
                            ? TomlItemType.Array
                            : TomlItemType.Table)
                        : obj is DateTimeOffset ? TomlItemType.Datetime
                        : obj is IEnumerable ? TomlItemType.Array
                        : TomlItemType.Table;
            }
        }

        private static XAttribute[] CreateTypeAttr(TomlItemType nodeType)
        {
            return new[]
            {
                new XAttribute("type", XUtils.GetJsonTypeString(nodeType)),
                new XAttribute("toml", nodeType.ToString())
            };
        }

        private static object ToXml(TomlItemType nodeType, object obj)
        {
            var dt = obj as DynamicToml;
            if (dt != null) return dt.element.Elements();

            switch (nodeType)
            {
                case TomlItemType.Array:
                    return ((IEnumerable)obj).Cast<object>().Select(o =>
                    {
                        var type = GetTomlType(o);
                        return new XElement("item", CreateTypeAttr(type), ToXml(type, o));
                    });
                case TomlItemType.Table:
                    var xd = new XDocument();
                    using (var xw = xd.CreateWriter())
                    {
                        var s = new DataContractJsonSerializer(obj.GetType());
                        s.WriteObject(xw, obj);
                    }
                    var root = new XElement(xd.Root);
                    foreach (var xe in root.Descendants())
                    {
                        var type = XUtils.GetTypeAttr(xe);
                        if (type == "null")
                            xe.Remove();
                        else
                            xe.SetAttributeValue("toml",
                                (type == "string" ? TomlItemType.BasicString
                                : type == "number" ? (xe.Value.Any(c => c == '.' || c == 'e' || c == 'E')
                                    ? TomlItemType.Float : TomlItemType.Integer)
                                : type == "boolean" ? TomlItemType.Boolean
                                : type == "array" ? TomlItemType.Array
                                : TomlItemType.Table).ToString()
                            );
                    }
                    return root.Elements();
                default:
                    return obj;
            }
        }

        /// <summary>
        /// Make a <see cref="DynamicToml"/> with the empty table.
        /// </summary>
        /// <returns>A <see cref="DynamicToml"/> with the empty table.</returns>
        public static dynamic CreateTable()
        {
            return new DynamicToml(new XElement("root", new XAttribute("type", "object"), new XAttribute("toml", "Table")));
        }

        /// <summary>
        /// Make a <see cref="DynamicToml"/> with the empty array.
        /// </summary>
        /// <returns>A <see cref="DynamicToml"/> with the empty array.</returns>
        public static dynamic CreateArray()
        {
            return new DynamicToml(new XElement("root", new XAttribute("type", "array"), new XAttribute("toml", "Array")));
        }

        /// <summary>
        /// Loads a <see cref="DynamicToml"/> from a <see cref="TextReader"/>.
        /// </summary>
        /// <param name="reader">A <see cref="TextReader" /> that will be read for the TOML content.</param>
        /// <returns>A <see cref="DynamicToml"/> that contains the TOML that was read from the specified <see cref="TextReader"/>.</returns>
        public static dynamic Parse(TextReader reader)
        {
            return new DynamicToml(TomlConvert.DeserializeXElement(reader));
        }

        /// <summary>
        /// Creates a new <see cref="DynamicToml"/> instance by using the specified stream.
        /// </summary>
        /// <param name="stream">The stream that contains the TOML data.</param>
        /// <returns>An <see cref="DynamicToml"/> object used to read the data that is contained in the stream.</returns>
        public static dynamic Parse(Stream stream)
        {
            return Parse(new StreamReader(stream));
        }

        /// <summary>
        /// Load an <see cref="DynamicToml"/> from a string that contains XML.
        /// </summary>
        /// <param name="toml">A string that contains TOML.</param>
        /// <returns>A <see cref="DynamicToml"/> populated from the string that contains TOML.</returns>
        public static dynamic Parse(IEnumerable<char> toml)
        {
            return new DynamicToml(TomlConvert.DeserializeXElement(toml));
        }

        private DynamicToml(XElement elm)
        {
            this.element = elm;
            this.isArray = XUtils.GetTomlAttr(elm) == TomlItemType.Array;
            if (this.isArray && elm.HasElements)
            {
                this.arrayType = elm.Elements().Aggregate(TomlItemType.None, (a, xe) =>
                {
                    var type = XUtils.GetTomlAttr(xe).Value.Normalize();
                    if (a == TomlItemType.None || a == type) return type;
                    throw new InvalidDataException("The values' type are not uniform.");
                });
            }
        }

        private readonly XElement element;
        private readonly bool isArray;
        private TomlItemType arrayType = TomlItemType.None;

        /// <summary>
        /// Gets a value indicating whether the <see cref="DynamicToml"/> represents a table.
        /// </summary>
        public bool IsObject
        {
            get
            {
                return !this.isArray;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="DynamicToml"/> represents an array.
        /// </summary>
        public bool IsArray
        {
            get
            {
                return this.isArray;
            }
        }

        private XElement Get(string key)
        {
            return this.element.Elements()
                .FirstOrDefault(xe => XUtils.GetKey(xe) == key);
        }

        private XElement Get(int index)
        {
            return this.element.Elements().ElementAtOrDefault(index);
        }

        /// <summary>
        /// Determines whether the specified key has been defined.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns><c>true</c> if the property named the specified key has been defined.</returns>
        public bool IsDefined(string key)
        {
            return !this.isArray && this.Get(key) != null;
        }

        /// <summary>
        /// Determines whether the element of the specified index has been defined.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns><c>true</c> if the element has been defined.</returns>
        public bool IsDefined(int index)
        {
            return this.isArray && this.Get(index) != null;
        }

        private bool Delete(XElement xe)
        {
            if (xe != null)
            {
                xe.Remove();
                this.EnsureArrayType();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Deletes the property.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns><c>true</c> if the element is successfully found and removed.</returns>
        public bool Delete(string key)
        {
            return this.Delete(this.Get(key));
        }

        /// <summary>
        /// Deletes the element.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns><c>true</c> if the element is successfully found and removed.</returns>
        public bool Delete(int index)
        {
            return this.Delete(this.Get(index));
        }

        /// <summary>
        /// Adds the specified value.
        /// </summary>
        /// <param name="obj">The value of the element to add.</param>
        public void Add(object obj)
        {
            if (!this.isArray)
                throw new InvalidOperationException("This is not an array.");

            var type = GetTomlType(obj);
            this.EnsureArrayType(type);
            var attr = CreateTypeAttr(type);
            var node = ToXml(type, obj);
            this.element.Add(new XElement("item", attr, node));
        }

        /// <summary>
        /// Adds the specified key and value.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="obj">The value of the element to add.</param>
        public void Add(string key, object obj)
        {
            if (this.isArray)
                throw new InvalidOperationException("This is not a table.");

            this.TrySetKeyValue(key, obj, true);
        }

        private void EnsureArrayType()
        {
            if (this.isArray && !this.element.HasElements)
                this.arrayType = TomlItemType.None;
        }

        private void EnsureArrayType(TomlItemType nodeType)
        {
            if (!this.isArray) return;

            var n = nodeType.Normalize();
            if (this.arrayType == TomlItemType.None)
                this.arrayType = n;
            else if (this.arrayType != n)
                throw new ArgumentException("The value is unmatched for the type of this array.");
        }

#pragma warning disable 1591 // Ignore XML comments

        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return this.isArray
                ? Enumerable.Empty<string>()
                : this.element.Elements().Select(XUtils.GetKey);
        }

        public override bool TryInvoke(InvokeBinder binder, object[] args, out object result)
        {
            result = null;
            if (args.Length != 1) return false;
            var arg = args[0];
            if (arg is int)
            {
                result = this.Delete((int)arg);
                return true;
            }
            if (!this.isArray && arg is string)
            {
                result = this.Delete((string)arg);
                return true;
            }
            return false;
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            if (args.Length > 0)
            {
                result = null;
                return false;
            }
            result = this.IsDefined(binder.Name);
            return true;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            if (this.isArray)
            {
                result = null;
                return false;
            }
            var xe = this.Get(binder.Name);
            if (xe == null)
            {
                result = null;
                return false;
            }
            result = ToValue(xe);
            return true;
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            result = null;
            if (indexes.Length != 1) return false;
            var index = indexes[0];
            XElement xe = null;
            if (index is int)
                xe = this.Get((int)index);
            else if (!this.isArray && index is string)
                xe = this.Get((string)index);
            else return false;
            if (xe == null) throw new KeyNotFoundException();
            result = ToValue(xe);
            return true;
        }

        private bool TrySetKeyValue(string key, object value, bool add)
        {
            var xe = this.Get(key);
            if (xe != null && add) throw new ArgumentException("An element with the same key already exists.");
            if (value == null)
            {
                if (xe != null)
                {
                    xe.Remove();
                    this.EnsureArrayType();
                }
                return true;
            }

            var type = GetTomlType(value);
            var attr = CreateTypeAttr(type);
            var node = ToXml(type, value);
            if (xe == null)
            {
                this.element.Add(XUtils.CreateElement(key, attr, node));
            }
            else
            {
                xe.SetAttributeValue("type", attr[0].Value);
                xe.SetAttributeValue("toml", attr[1].Value);
                xe.ReplaceNodes(node);
            }
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            if (this.isArray) return false;
            return this.TrySetKeyValue(binder.Name, value, false);
        }

        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value)
        {
            if (indexes.Length != 1) return false;
            var index = indexes[0];
            if (index is int)
            {
                var i = (int)index;
                var elements = this.element.Elements().ToArray();
                if (elements.Length < i)
                    throw new ArgumentOutOfRangeException();

                if (value == null)
                {
                    if (elements.Length > i)
                    {
                        elements[i].Remove();
                        this.EnsureArrayType();
                    }
                    return true;
                }

                var type = GetTomlType(value);
                this.EnsureArrayType(type);
                var attr = CreateTypeAttr(type);
                var node = ToXml(type, value);
                if (elements.Length == i)
                {
                    this.element.Add(new XElement("item", attr, node));
                }
                else
                {
                    var xe = elements[i];
                    xe.SetAttributeValue("type", attr[0].Value);
                    xe.SetAttributeValue("toml", attr[1].Value);
                    xe.ReplaceNodes(node);
                }
                return true;
            }
            if (!this.isArray && index is string)
                return this.TrySetKeyValue((string)index, value, false);

            return false;
        }

        private IEnumerable<KeyValuePair<string, object>> ToKvpEnumerable()
        {
            return this.element.Elements().Select(xe =>
                new KeyValuePair<string, object>(XUtils.GetKey(xe), ToValue(xe)));
        }

        private IEnumerable<object> ToEnumerable()
        {
            return this.isArray
                ? this.element.Elements().Select(ToValue)
                : this.ToKvpEnumerable().Cast<object>();
        }

        IEnumerator<object> IEnumerable<object>.GetEnumerator()
        {
            return this.ToEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.ToEnumerable().GetEnumerator();
        }

        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            if (binder.Type.IsAssignableFrom(typeof(List<object>)))
            {
                result = this.ToEnumerable().ToList();
                return true;
            }
            if (binder.Type.IsAssignableFrom(typeof(ArrayList)))
            {
                var al = new ArrayList();
                foreach (var o in this.ToEnumerable())
                    al.Add(o);
                result = al;
                return true;
            }
            if (binder.Type.IsAssignableFrom(typeof(object[])))
            {
                result = this.ToEnumerable().ToArray();
                return true;
            }
            if (!this.isArray)
            {
                if (binder.Type.IsAssignableFrom(typeof(List<KeyValuePair<string, object>>)))
                {
                    result = this.ToKvpEnumerable().ToList();
                    return true;
                }
                if (binder.Type.IsAssignableFrom(typeof(Dictionary<string, object>)))
                {
                    var dic = new Dictionary<string, object>();
                    foreach (var xe in this.element.Elements())
                        dic.Add(XUtils.GetKey(xe), ToValue(xe));
                    result = dic;
                    return true;
                }
                if (binder.Type.IsAssignableFrom(typeof(Hashtable)))
                {
                    var ht = new Hashtable();
                    foreach (var xe in this.element.Elements())
                        ht.Add(XUtils.GetKey(xe), ToValue(xe));
                    result = ht;
                    return true;
                }
                if (binder.Type.IsAssignableFrom(typeof(KeyValuePair<string, object>[])))
                {
                    result = this.ToKvpEnumerable().ToArray();
                    return true;
                }
            }
            if (binder.Type.IsAssignableFrom(typeof(XElement)))
            {
                result = this.element;
                return true;
            }
            result = null;
            return false;
        }

        public override bool Equals(object obj)
        {
            var dt = obj as DynamicToml;
            return dt != null && this.element == dt.element;
        }

        public override int GetHashCode()
        {
            return this.element.GetHashCode();
        }

#pragma warning restore 1591

        /// <summary>
        /// Writes the content of <see cref="DynamicToml"/> to the specified <see cref="TextWriter"/>.
        /// </summary>
        /// <param name="writer">The <see cref="TextWriter"/> to write this <see cref="DynamicToml"/> to.</param>
        public void WriteTo(TextWriter writer)
        {
            XUtils.WriteTo(this.element, writer);
        }

        /// <summary>
        /// Writes the content of <see cref="DynamicToml"/> to the specified stream.
        /// </summary>
        /// <param name="stream">The stream to write this <see cref="DynamicToml"/> to.</param>
        public void WriteTo(Stream stream)
        {
            WriteTo(new StreamWriter(stream));
        }

        /// <summary>
        /// Returns the TOML for this element.
        /// </summary>
        /// <returns>A <see cref="String"/> containing the TOML.</returns>
        public override string ToString()
        {
            return XUtils.GetStreamString(this.WriteTo);
        }
    }
}
