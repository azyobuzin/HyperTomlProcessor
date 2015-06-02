using System;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace HyperTomlProcessor
{
    internal static class XUtils
    {
        static XUtils()
        {
            InitializeValidNameTable();
        }

        internal static readonly XNamespace NamespaceA = "item";
        internal static XAttribute PrefixA
        {
            get
            {
                return new XAttribute(XNamespace.Xmlns + "a", NamespaceA);
            }
        }

        internal static XElement CreateElement(string name, params object[] content)
        {
            return IsValidName(name) ? new XElement(name, content)
                : new XElement(NamespaceA + "item", PrefixA, new XAttribute("item", name), content);
        }

        internal static string GetKey(XElement xe)
        {
            return xe.Name.Namespace == NamespaceA
                ? xe.Attribute("item").Value : xe.Name.LocalName;
        }

        private static bool[] ValidFirstName;
        private static bool[] ValidName;
        private static void Allow(bool[] a, int start, int end)
        {
            for (var i = start; i <= end; i++)
                a[i] = true;
        }
        private static void InitializeValidNameTable()
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
        internal static bool IsValidName(string name)
        {
            var bytes = Encoding.UTF8.GetBytes(name);
            if (!ValidFirstName[bytes[0]]) return false;
            foreach (var b in bytes)
            {
                if (!ValidName[b]) return false;
            }
            return true;
        }

        internal static string GetJsonTypeString(TomlItemType type)
        {
            switch (type)
            {
                case TomlItemType.BasicString:
                case TomlItemType.MultilineBasicString:
                case TomlItemType.LiteralString:
                case TomlItemType.MultilineLiteralString:
                case TomlItemType.Datetime:
                    return "string";
                case TomlItemType.Integer:
                case TomlItemType.Float:
                    return "number";
                case TomlItemType.Boolean:
                    return "boolean";
                case TomlItemType.Array:
                    return "array";
                case TomlItemType.Table:
                case TomlItemType.InlineTable:
                    return "object";
                default:
                    return "null";
            }
        }

        internal static string GetTypeAttr(XElement xe)
        {
            var type = xe.Attribute("type");
            return type != null ? type.Value : "string";
        }

        internal static TomlItemType? GetTomlAttr(XElement xe)
        {
            var toml = xe.Attribute("toml");
            return toml != null ? (TomlItemType?)Enum.Parse(typeof(TomlItemType), toml.Value) : null;
        }

        internal static string GetStreamString(Action<StreamWriter> write)
        {
            using (var stream = new MemoryStream())
            {
                var writer = new StreamWriter(stream);
                write(writer);
                writer.Flush();
                stream.Seek(0, SeekOrigin.Begin);
                return new StreamReader(stream).ReadToEnd();
            }
        }
    }
}
