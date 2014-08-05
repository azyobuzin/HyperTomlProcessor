using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
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
                    return "object";
                default:
                    return "null";
            }
        }

        private static string GetTypeAttr(XElement xe)
        {
            var type = xe.Attribute("type");
            if (type == null)
                throw new SerializationException("'type' attribute must exist.");
            return type.Value;
        }

        internal static TomlItemType? GetTomlAttr(XElement xe)
        {
            var toml = xe.Attribute("toml");
            return toml != null ? (TomlItemType?)Enum.Parse(typeof(TomlItemType), toml.Value) : null;
        }

        internal static void WriteTo(XElement xe, TextWriter writer)
        {
            switch (GetTypeAttr(xe))
            {
                case "object":
                    SerializeObject(xe, writer);
                    break;
                case "array":
                    if (IsArrayOfTable(xe))
                        SerializeArrayOfTable(xe, writer);
                    else
                        SerializeArray(xe, writer);
                    break;
                default:
                    throw new SerializationException("Invalid 'type' attribute.");
            }
        }

        private static bool IsArrayOfTable(XElement xe)
        {
            return xe.HasElements && GetTypeAttr(xe.Elements().First()) == "object";
        }

        private static string GetFullName(XElement xe)
        {
            var s = new Stack<string>();
            while (xe.Parent != null)
            {
                if (GetTypeAttr(xe.Parent) != "array")
                    s.Push(GetKey(xe));
                xe = xe.Parent;
            }
            return string.Join(".", s);
        }

        private static void WriteKey(XElement xe, TextWriter writer)
        {
            writer.Write(GetKey(xe));
            writer.Write(" = ");
        }

        private static string BasicEscape(string s)
        {
            return string.Concat(s.Select(c =>
            {
                switch (c)
                {
                    case '\b':
                        return "\\b";
                    case '\t':
                        return "\\t";
                    case '\f': // これはマルチラインでどうするべき？
                        return "\\f";
                    case '"':
                        return "\\\"";
                    case '\\':
                        return "\\\\";
                }
                if (0 <= c && c <= 0x1F)
                    return "\\u" + ((int)c).ToString("X4");
                return c.ToString();
            }));
        }

        private static void WriteString(XElement xe, TextWriter writer)
        {
            switch (GetTomlAttr(xe))
            {
                case TomlItemType.BasicString:
                    WriteBasicString(xe.Value, writer);
                    break;
                case TomlItemType.MultilineBasicString:
                    writer.Write("\"\"\"");
                    writer.Write(BasicEscape(xe.Value));
                    writer.Write("\"\"\"");
                    break;
                case TomlItemType.LiteralString:
                    foreach (var c in xe.Value)
                    {
                        if (c == '\r' || c == '\n')
                            throw new SerializationException("A literal string cannot contain newlines.");
                        else if (c == '\'')
                            throw new SerializationException("A literal string cannot contain single quotes.");
                    }
                    writer.Write('\'');
                    writer.Write(xe.Value);
                    writer.Write('\'');
                    break;
                case TomlItemType.MultilineLiteralString:
                    if (xe.Value.Contains("'''"))
                        throw new SerializationException("A multi-line literal string cannot contain \"'''\"");
                    break;
                case TomlItemType.Datetime:
                    WriteDateTime((DateTimeOffset)xe, writer);
                    break;
                default: // BasicString or Datetime
                    try
                    {
                        WriteDateTime((DateTimeOffset)xe, writer);
                    }
                    catch (FormatException)
                    {
                        WriteBasicString(xe.Value, writer);
                    }
                    break;
            }
        }

        private static void WriteBasicString(string value, TextWriter writer)
        {
            writer.Write('\"');
            writer.Write(BasicEscape(value).Replace("\r", "\\r").Replace("\n", "\\n"));
            writer.Write('\"');
        }

        private static void WriteDateTime(DateTimeOffset d, TextWriter writer)
        {
            // "Datetimes are ISO 8601 dates, but only the full zulu form is allowed."
            writer.Write(d.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));
        }

        private static void WriteNumber(XElement xe, TextWriter writer)
        {
            switch (GetTomlAttr(xe))
            {
                case TomlItemType.Integer:
                    WriteInteger((long)xe, writer);
                    break;
                case TomlItemType.Float:
                    WriteFloat((double)xe, writer);
                    break;
                default:
                    var i = (long)xe;
                    var f = (double)xe;
                    if (i == f) WriteInteger(i, writer);
                    else WriteFloat(f, writer);
                    break;
            }
        }

        private static void WriteInteger(long i, TextWriter writer)
        {
            writer.Write(i.ToString("D"));
        }

        private static void WriteFloat(double f, TextWriter writer)
        {
            var s = f.ToString("F99").TrimEnd('0');
            if (s[s.Length - 1] == '.') s += '0';
            writer.Write(s);
        }

        private static void WriteBoolean(XElement xe, TextWriter writer)
        {
            writer.Write((bool)xe ? "true" : "false");
        }

        private static void WriteComment(XComment xc, TextWriter writer)
        {
            writer.Write('#');
            writer.WriteLine(xc.Value);
        }

        private static void SerializeObject(XElement xe, TextWriter writer)
        {
            // Table と Array of Table は後に
            var ordered = xe.Nodes().OrderBy(n =>
            {
                var e = n as XElement;
                if (e != null)
                {
                    var type = GetTypeAttr(e);
                    if (type == "object") return true;
                    if (type == "array" && IsArrayOfTable(e))
                        return true;
                }
                return false;
            });
            foreach (var n in ordered)
            {
                var e = n as XElement;
                if (e != null)
                {
                    switch (GetTypeAttr(e))
                    {
                        case "string":
                            WriteKey(e, writer);
                            WriteString(e, writer);
                            writer.WriteLine();
                            break;
                        case "number":
                            WriteKey(e, writer);
                            WriteNumber(e, writer);
                            writer.WriteLine();
                            break;
                        case "boolean":
                            WriteKey(e, writer);
                            WriteBoolean(e, writer);
                            writer.WriteLine();
                            break;
                        case "object":
                            writer.WriteLine("[{0}]", GetFullName(e));
                            SerializeObject(e, writer);
                            break;
                        case "array":
                            if (IsArrayOfTable(e))
                                SerializeArrayOfTable(e, writer);
                            else
                            {
                                WriteKey(e, writer);
                                SerializeArray(e, writer);
                                writer.WriteLine();
                                break;
                            }
                            break;
                        default:
                            // ignore null
                            break;
                    }
                }
                else
                {
                    var c = n as XComment;
                    if (c != null)
                        WriteComment(c, writer);
                    else
                        throw new SerializationException("Unknown XNode.");
                }
            }
        }

        private static bool ExistsNextArrayItem(XNode xn)
        {
            while (xn.NextNode != null)
            {
                xn = xn.NextNode;
                var xe = xn as XElement;
                if (xe != null && GetTypeAttr(xe) != "null")
                    return true;
            }
            return false;
        }

        private static void SerializeArray(XElement xe, TextWriter writer)
        {
            writer.Write('[');
            foreach (var n in xe.Nodes())
            {
                var e = n as XElement;
                if (e != null)
                {
                    switch (GetTypeAttr(e))
                    {
                        case "string":
                            WriteString(e, writer);
                            break;
                        case "number":
                            WriteNumber(e, writer);
                            break;
                        case "boolean":
                            WriteBoolean(e, writer);
                            break;
                        case "object":
                            throw new SerializationException("An array cannot contain tables.");
                        case "array":
                            SerializeArray(e, writer);
                            break;
                        default:
                            // ignore null
                            continue;
                    }
                    if (ExistsNextArrayItem(n))
                        writer.Write(", ");
                }
                else
                {
                    var c = n as XComment;
                    if (c != null)
                        WriteComment(c, writer);
                    else
                        throw new SerializationException("Unknown XNode.");
                }
            }
            writer.Write(']');
        }

        private static void SerializeArrayOfTable(XElement xe, TextWriter writer)
        {
            foreach (var n in xe.Nodes())
            {
                var e = n as XElement;
                if (e != null)
                {
                    writer.WriteLine("[[{0}]]", GetFullName(e));
                    SerializeObject(e, writer);
                }
                else
                {
                    var c = n as XComment;
                    if (c != null)
                        WriteComment(c, writer);
                    else
                        throw new SerializationException("Unknown XNode.");
                }
            }
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
