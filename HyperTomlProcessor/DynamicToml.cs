using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Xml.Linq;

namespace HyperTomlProcessor
{
    public class DynamicToml : DynamicObject, IEnumerable<object>
    {
        private static object ToValue(XElement xe)
        {
            switch (xe.Attribute("toml").Value)
            {
                case "basicString":
                case "multi-lineBasicString":
                case "literalString":
                case "multi-lineLiteralString":
                    return xe.Value;
                case "integer":
                    return (long)xe;
                case "float":
                    return (double)xe;
                case "boolean":
                    return (bool)xe;
                case "datetime":
                    return (DateTimeOffset)xe;
                default:
                    return new DynamicToml(xe);
            }
        }

        private static TomlNodeType GetTomlType(object obj)
        {

            if (obj == null) throw new ArgumentNullException("obj");

            switch (Type.GetTypeCode(obj.GetType()))
            {
                case TypeCode.Boolean:
                    return TomlNodeType.Boolean;
                case TypeCode.String:
                case TypeCode.Char:
                    return TomlNodeType.BasicString;
                case TypeCode.DateTime:
                    return TomlNodeType.Datetime;
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return TomlNodeType.Float;
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return TomlNodeType.Integer;
                default:
                    //TODO: Dictionary に対応
                    var dt = obj as DynamicToml;
                    return dt != null
                        ? (dt.isArray
                            ? TomlNodeType.StartArray // arrayOfTable も array にしちゃえ
                            : TomlNodeType.StartTable)
                        : obj is DateTimeOffset ? TomlNodeType.Datetime
                        : obj is IEnumerable ? TomlNodeType.StartArray
                        : TomlNodeType.StartTable;
            }
        }

        private static XAttribute[] CreateTypeAttr(TomlNodeType nodeType)
        {
            return new[]
            {
                new XAttribute("type", XUtils.GetJsonTypeString(nodeType)),
                new XAttribute("toml", XUtils.TomlTypeTable[nodeType])
            };
        }

        private static object ToXml(TomlNodeType nodeType, object obj)
        {
            var dt = obj as DynamicToml;
            if (dt != null) return dt.element.Elements();

            switch (nodeType)
            {
                case TomlNodeType.StartArray:
                case TomlNodeType.StartArrayOfTable:
                    return ((IEnumerable)obj).Cast<object>().Select(o =>
                    {
                        var type = GetTomlType(o);
                        return new XStreamingElement("item", CreateTypeAttr(type), ToXml(type, o));
                    });
                case TomlNodeType.StartTable:
                    return obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                        .Select(p => new { Name = p.Name, Value = p.GetValue(obj, null) })
                        .Where(x => x.Value != null) // null cannot be serialized
                        .Select(x =>
                        {
                            var type = GetTomlType(x.Value);
                            return new XStreamingElement(x.Name, CreateTypeAttr(type), ToXml(type, x.Value));
                        });
                default:
                    return new XText(obj.ToString());
            }
        }

        private static TomlNodeType NormalizeType(TomlNodeType nodeType)
        {
            switch (nodeType)
            {
                case TomlNodeType.MultilineBasicString:
                case TomlNodeType.LiteralString:
                case TomlNodeType.MultilineLiteralString:
                    return TomlNodeType.BasicString;
                case TomlNodeType.StartArrayOfTable:
                    return TomlNodeType.StartArray;
            }
            return nodeType;
        }

        private static TomlNodeType GetNormalizedType(string tomlAttr)
        {
            switch (tomlAttr)
            {
                case "basicString":
                case "multi-lineBasicString":
                case "literalString":
                case "multi-lineLiteralString":
                    return TomlNodeType.BasicString;
                case "integer":
                    return TomlNodeType.Integer;
                case "float":
                    return TomlNodeType.Float;
                case "boolean":
                    return TomlNodeType.Boolean;
                case "datetime":
                    return TomlNodeType.Datetime;
                case "array":
                case "arrayOfTable":
                    return TomlNodeType.StartArray;
                case "table":
                    return TomlNodeType.StartTable;
                default:
                    throw new ArgumentException("tomlAttr is invalid.");
            }
        }

        public static dynamic CreateTable()
        {
            return new DynamicToml(new XElement("root", new XAttribute("type", "object"), new XAttribute("toml", "table")));
        }

        public static dynamic CreateArray()
        {
            return new DynamicToml(new XElement("root", new XAttribute("type", "array"), new XAttribute("toml", "array")));
        }

        public static dynamic Parse(XmlTomlReader reader)
        {
            try
            {
                return new DynamicToml(XElement.Load(reader));
            }
            finally
            {
                reader.Close();
            }
        }

        public static dynamic Parse(TextReader reader)
        {
            return Parse(new XmlTomlReader(reader));
        }

        public static dynamic Parse(TomlReader reader)
        {
            return Parse(new XmlTomlReader(reader));
        }

        public static dynamic Parse(string toml)
        {
            return Parse(new StringReader(toml));
        }

        public static void Serialize(object obj, TextWriter writer)
        {
            var type = GetTomlType(obj);
            var elm = new XElement("root", CreateTypeAttr(type), ToXml(type, obj));
            XUtils.WriteTo(elm, writer);
        }

        public static string Serialize(object obj)
        {
            return XUtils.GetStreamString(w => Serialize(obj, w));
        }

        private DynamicToml(XElement elm)
        {
            this.element = elm;
            this.isArray = elm.Attribute("toml").Value != "table";
            if (this.isArray && elm.HasElements)
            {
                this.arrayType = elm.Elements().Aggregate(TomlNodeType.None, (a, xe) =>
                {
                    var type = GetNormalizedType(xe.Attribute("toml").Value);
                    if (a == TomlNodeType.None || a == type) return type;
                    throw new InvalidDataException("The values' type are not uniform.");
                });
            }
        }

        private readonly XElement element;
        private readonly bool isArray;
        private TomlNodeType arrayType = TomlNodeType.None;

        public bool IsObject
        {
            get
            {
                return !this.isArray;
            }
        }

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

        public bool IsDefined(string key)
        {
            return !this.isArray && this.Get(key) != null;
        }

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

        public bool Delete(string key)
        {
            return this.Delete(this.Get(key));
        }

        public bool Delete(int index)
        {
            return this.Delete(this.Get(index));
        }

        public void Add(object obj)
        {
            if (!this.isArray)
                throw new InvalidOperationException("This is not an array.");

            var type = GetTomlType(obj);
            this.EnsureArrayType(type);
            var attr = CreateTypeAttr(type);
            var node = ToXml(type, obj);
            this.element.Add(new XStreamingElement("item", attr, node));
        }

        public void Add(string key, object obj)
        {
            if (this.isArray)
                throw new InvalidOperationException("This is not a table.");

            this.TrySetKeyValue(key, obj);
        }

        private void EnsureArrayType()
        {
            if (this.isArray && !this.element.HasElements)
                this.arrayType = TomlNodeType.None;
        }

        private void EnsureArrayType(TomlNodeType nodeType)
        {
            if (!this.isArray) return;

            var n = NormalizeType(nodeType);
            if (this.arrayType == TomlNodeType.None)
                this.arrayType = n;
            else if (this.arrayType != n)
                throw new ArgumentException("The value is unmatched for the type of this array.");
        }

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

        private bool TrySetKeyValue(string key, object value)
        {
            var xe = this.Get(key);
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
                var elm = XUtils.IsValidName(key)
                    ? new XStreamingElement(key, attr, node)
                    : new XStreamingElement(
                        XUtils.NamespaceA + "item",
                        XUtils.PrefixA,
                        new XAttribute("item", key),
                        attr,
                        node
                    );
                this.element.Add(elm);
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
            return this.TrySetKeyValue(binder.Name, value);
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
                    this.element.Add(new XStreamingElement("item", attr, node));
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
                return this.TrySetKeyValue((string)index, value);

            return false;
        }

        private IEnumerable<object> ToEnumerable()
        {
            return this.isArray
                ? this.element.Elements().Select(ToValue)
                : this.element.Elements().Select(xe =>
                    (object)new KeyValuePair<string, object>(XUtils.GetKey(xe), ToValue(xe)));
        }

        IEnumerator<object> IEnumerable<object>.GetEnumerator()
        {
            return this.ToEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.ToEnumerable().GetEnumerator();
        }

        private object Deserialize(Type type)
        {
            if (this.isArray)
            {
                return type.IsArray
                    ? this.DeserializeArray(type)
                    : this.DeserializeCollection(type);
            }
            else
            {
                Type keyType;
                Type valueType;
                if (ReflectionUtils.TryGetDictionaryType(type, out keyType, out valueType)
                    && keyType.IsAssignableFrom(typeof(string)))
                {
                    try
                    {
                        return this.DeserializeDictionary(type, keyType, valueType);
                    }
                    catch
                    { }
                }
                return this.DeserializeObject(type);
            }
        }

        private object DeserializeValue(XElement xe, Type type)
        {
            var value = ToValue(xe);
            var dt = value as DynamicToml;
            if (dt != null)
            {
                if (type == typeof(DynamicToml))
                    return dt;
                value = dt.Deserialize(type);
            }

            var valueType = value.GetType();
            if (type.IsAssignableFrom(valueType))
                return value;
            if (typeof(IConvertible).IsAssignableFrom(type))
                return Convert.ChangeType(value, type);
            throw new SerializationException("Could not convert to " + type.Name);
        }

        private object DeserializeArray(Type type)
        {
            var elm = this.element.Elements().ToArray();
            var elmType = type.GetElementType();
            var array = new object[elm.Length];
            for (var i = 0; i < elm.Length; i++)
                array[i] = DeserializeValue(elm[i], elmType);
            var result = Array.CreateInstance(elmType, elm.Length);
            Array.Copy(array, result, elm.Length); // faster than dynamic
            return result;
        }

        private object DeserializeCollection(Type type)
        {
            dynamic collection;
            var elmType = ReflectionUtils.GetCollectionType(type);
            if (type.IsAssignableFrom(typeof(List<object>)))
                collection = new List<object>();
            else if (type.IsAssignableFrom(typeof(ArrayList)))
                collection = new ArrayList();
            else if (type.IsInterface)
            {
                if (!typeof(IEnumerable).IsAssignableFrom(type))
                    throw new SerializationException(type.Name + " does not implement IEnumerable.");

                var listType = typeof(List<>).MakeGenericType(elmType);
                if (!type.IsAssignableFrom(listType))
                    throw new SerializationException("Could not make List<" + elmType.Name + ">.");
                collection = Activator.CreateInstance(listType);
            }
            else
                collection = Activator.CreateInstance(type);

            foreach (var xe in this.element.Elements())
                collection.Add((dynamic)DeserializeValue(xe, elmType));

            return collection;
        }

        private object DeserializeObject(Type type)
        {
            var result = Activator.CreateInstance(type);
            var dict = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0)
                .ToDictionary(p => p.Name);
            foreach (var xe in this.element.Elements())
            {
                var key = XUtils.GetKey(xe);
                if (dict.ContainsKey(key))
                {
                    var p = dict[key];
                    var isDynamic = p.GetSetMethod().GetParameters()[0]
                        .GetCustomAttributes(typeof(DynamicAttribute), false).Length != 0;
                    p.SetValue(result, DeserializeValue(xe, isDynamic ? typeof(DynamicToml) : p.PropertyType), null);
                }
            }
            return result;
        }

        private object DeserializeDictionary(Type type, Type keyType, Type valueType)
        {
            dynamic dic; // なんで ID<TKey, TValue> が ID を継承してないんじゃ！！
            if (type.IsAssignableFrom(typeof(Dictionary<string, object>)))
                dic = new Dictionary<string, object>();
            else if (type.IsAssignableFrom(typeof(Hashtable)))
                dic = new Hashtable();
            else if (type.IsInterface)
            {
                var dicType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
                if (!type.IsAssignableFrom(dicType))
                    throw new SerializationException(string.Format("Could not make Dictionary<{0}, {1}>.", keyType.Name, valueType.Name));
                dic = Activator.CreateInstance(dicType);
            }
            else
                dic = Activator.CreateInstance(type);

            foreach (var xe in this.element.Elements())
                dic.Add(XUtils.GetKey(xe), (dynamic)DeserializeValue(xe, valueType));

            return dic;
        }

        public T Deserialize<T>()
        {
            return (T)this.Deserialize(typeof(T));
        }

        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            try
            {
                result = this.Deserialize(binder.Type);
                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidCastException("Could not convert.", ex);
            }
        }

        public void WriteTo(TextWriter writer)
        {
            XUtils.WriteTo(this.element, writer);
        }

        public override string ToString()
        {
            return XUtils.GetStreamString(this.WriteTo);
        }
    }
}
