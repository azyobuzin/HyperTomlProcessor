using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Linq;

namespace HyperTomlProcessor
{
    internal class TomlWriter
    {
        public TomlWriter(TomlVersion version)
        {
            this.version = version;
        }

        private TomlVersion version;

        internal void WriteTo(XElement xe, TextWriter writer)
        {
            switch (XUtils.GetTypeAttr(xe))
            {
                case "object":
                    if (this.version >= TomlVersion.V04 && XUtils.GetTomlAttr(xe) == TomlItemType.InlineTable)
                        throw new SerializationException("Invalid 'type' attribute.");
                    this.SerializeObject(xe, writer);
                    break;
                case "array":
                    if (IsArrayOfTable(xe))
                        this.SerializeArrayOfTable(xe, writer);
                    else
                        this.SerializeArray(xe, writer);
                    break;
                default:
                    throw new SerializationException("Invalid 'type' attribute.");
            }
        }

        private static bool IsArrayOfTable(XElement xe)
        {
            return xe.HasElements && XUtils.GetTypeAttr(xe.Elements().First()) == "object";
        }

        private string GetFullName(XElement xe)
        {
            var s = new Stack<string>();
            while (xe.Parent != null)
            {
                if (XUtils.GetTypeAttr(xe.Parent) != "array")
                {
                    var key = XUtils.GetKey(xe);
                    if (this.version >= TomlVersion.V04 && !IsValidBareKey(key))
                        key = CreateBasicString(key);
                    s.Push(XUtils.GetKey(xe));
                }
                xe = xe.Parent;
            }
            return string.Join(".", s);
        }

        private static bool IsValidBareKey(string s)
        {
            return !s.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-');
        }

        private void WriteKey(XElement xe, TextWriter writer)
        {
            var key = XUtils.GetKey(xe);
            if (this.version >= TomlVersion.V04 && !IsValidBareKey(key))
                WriteBasicString(key, writer);
            else
                writer.Write(key);
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
                if (0 <= c && c <= 0x1F && c != '\r' && c != '\n')
                    return "\\u" + ((int)c).ToString("X4");
                return c.ToString();
            }));
        }

        private static void WriteString(XElement xe, TextWriter writer)
        {
            switch (XUtils.GetTomlAttr(xe))
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

        private static string CreateBasicString(string value)
        {
            return string.Format("\"{0}\"", BasicEscape(value).Replace("\r", "\\r").Replace("\n", "\\n"));
        }

        private static void WriteBasicString(string value, TextWriter writer)
        {
            writer.Write(CreateBasicString(value));
        }

        private static void WriteDateTime(DateTimeOffset d, TextWriter writer)
        {
            writer.Write(XmlConvert.ToString(d));
        }

        private static void WriteNumber(XElement xe, TextWriter writer)
        {
            switch (XUtils.GetTomlAttr(xe))
            {
                case TomlItemType.Integer:
                    WriteInteger((long)xe, writer);
                    break;
                case TomlItemType.Float:
                    WriteFloat((double)xe, writer);
                    break;
                default:
                    try
                    {
                        WriteInteger((long)xe, writer);
                    }
                    catch (FormatException)
                    {
                        WriteFloat((double)xe, writer);
                    }
                    break;
            }
        }

        private static void WriteInteger(long i, TextWriter writer)
        {
            writer.Write(i.ToString("D"));
        }

        private static void WriteFloat(double f, TextWriter writer)
        {
            if (double.IsNaN(f))
                throw new SerializationException("NaN is not allowed.");
            if (double.IsInfinity(f))
                throw new SerializationException("Infinity is not allowed.");
            writer.Write(XmlConvert.ToString(f));
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

        private void WriteInlineTable(XElement xe, TextWriter writer)
        {
            writer.Write('{');
            foreach (var n in xe.Nodes())
            {
                var e = n as XElement;
                if (e != null)
                {
                    WriteKey(e, writer);
                    switch (XUtils.GetTypeAttr(e))
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
                            WriteInlineTable(e, writer);
                            break;
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
                    if (n is XComment)
                        throw new SerializationException("A comment is not allowed in an inline table.");
                    throw new SerializationException("Unknown XNode.");
                }
            }
            writer.Write('}');
        }

        private void SerializeObject(XElement xe, TextWriter writer)
        {
            // Table と Array of Table は後に
            var ordered = xe.Nodes().OrderBy(n =>
            {
                var e = n as XElement;
                if (e != null)
                {
                    var type = XUtils.GetTypeAttr(e);
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
                    switch (XUtils.GetTypeAttr(e))
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
                            if (this.version >= TomlVersion.V04 && XUtils.GetTomlAttr(e) == TomlItemType.InlineTable)
                            {
                                WriteKey(e, writer);
                                WriteInlineTable(e, writer);
                                writer.WriteLine();
                            }
                            else
                            {
                                writer.WriteLine("[{0}]", GetFullName(e));
                                SerializeObject(e, writer);
                            }
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
                if (xe != null && XUtils.GetTypeAttr(xe) != "null")
                    return true;
            }
            return false;
        }

        private void SerializeArray(XElement xe, TextWriter writer)
        {
            writer.Write('[');
            foreach (var n in xe.Nodes())
            {
                var e = n as XElement;
                if (e != null)
                {
                    switch (XUtils.GetTypeAttr(e))
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
                            if (this.version >= TomlVersion.V04)
                            {
                                WriteInlineTable(e, writer);
                                break;
                            }
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

        private void SerializeArrayOfTable(XElement xe, TextWriter writer)
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
    }
}
