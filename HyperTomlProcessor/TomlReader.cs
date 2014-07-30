using System;
using System.IO;
using System.Linq;
using System.Text;

namespace HyperTomlProcessor
{
    public enum TomlNodeType
    {
        None,
        EndLine,
        Comment,
        Key,
        BasicString,
        MultilineBasicString,
        LiteralString,
        MultilineLiteralString,
        Integer,
        Float,
        Boolean,
        Datetime,
        StartArray,
        EndArray,
        StartTable,
        StartArrayOfTable
    }

    public class TomlReader : IDisposable
    {
        public TomlReader(TextReader reader)
        {
            if (reader == null) throw new ArgumentNullException("reader");
            this.reader = reader;
        }

        private readonly TextReader reader;
        private bool disposed = false;
        private int currentChar;
        private bool expectValue = false;
        private bool expectEndLine = false;
        private int arrayDim = 0;

        public TomlNodeType NodeType { get; private set; }
        public object Value { get; private set; }
        public int LineNumber { get; private set; }
        public int LinePosition { get; private set; }

        private void ReadChar()
        {
            var old = this.currentChar;
            this.currentChar = this.reader.Read();
            if (this.currentChar != -1)
            {
                this.LinePosition++;
                if (this.LineNumber == 0) this.LineNumber = 1;
            }
            if (this.currentChar == '\r' || this.currentChar == '\n')
            {
                if (this.currentChar != '\n' || old != '\r')
                {
                    this.LineNumber++;
                    this.LinePosition = 0;
                }
            }
        }

        private string ReadChars(int count)
        {
            var sb = new StringBuilder(count);
            for (var i = 0; i < count; i++)
            {
                this.ReadChar();
                if (this.currentChar == -1)
                    throw new TomlException(this, "EOF");
                if (this.currentChar == '\r' || this.currentChar == '\n')
                    throw new TomlException(this, "The newline is not allowed at this position.");
                sb.Append((char)this.currentChar);
            }
            return sb.ToString();
        }

        public bool Read()
        {
            this.NodeType = TomlNodeType.None;
            this.Value = null;

            this.MoveNext();
            if (this.currentChar == -1) return false;

            switch (this.currentChar)
            {
                case '\r':
                case '\n':
                    this.ReadEndLine();
                    this.expectEndLine = false;
                    return true;
                case '#':
                    this.ReadComment();
                    return true;
            }

            if (this.expectEndLine)
                throw new TomlException(this, "EndLine is expected.");

            if (this.expectValue)
            {
                switch (this.currentChar)
                {
                    case '"':
                        this.ReadBasicString();
                        break;
                    case '\'':
                        this.ReadLiteralString();
                        break;
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                    case '-':
                        this.ReadNumber();
                        break;
                    case 't':
                    case 'f':
                        this.ReadBoolean();
                        break;
                    case '[':
                        this.NodeType = TomlNodeType.StartArray;
                        this.arrayDim++;
                        this.expectValue = true;
                        break;
                    case ']':
                        this.NodeType = TomlNodeType.EndArray;
                        this.arrayDim--;
                        break;
                    default:
                        throw new TomlException(this, "Unrecognized token.");
                }
                if (this.arrayDim == 0)
                {
                    this.expectValue = false;
                    this.expectEndLine = true;
                }
            }
            else
            {
                switch (this.currentChar)
                {
                    case '[':
                        this.ReadStartTable();
                        this.expectEndLine = true;
                        break;
                    default:
                        this.ReadKey();
                        this.expectValue = true;
                        break;
                }
            }

            return true;
        }

        private void MoveNext()
        {
            var commaCount = 0;
            while (true)
            {
                this.ReadChar();
                if (this.arrayDim > 0 && this.currentChar == ',')
                {
                    if (commaCount > 0)
                        throw new TomlException(this, "Too many commas.");
                    commaCount++;
                }
                else if (this.currentChar != ' ' && this.currentChar != '\t')
                    return;
            }
        }

        private void ReadEndLine()
        {
            this.NodeType = TomlNodeType.EndLine;
            if (this.currentChar == '\r')
            {
                var next = this.reader.Peek();
                if (next == '\n')
                {
                    this.ReadChar();
                    this.Value = "\r\n";
                    return;
                }
            }
            this.Value = ((char)this.currentChar).ToString();
        }

        private void ReadComment()
        {
            this.NodeType = TomlNodeType.Comment;
            var sb = new StringBuilder();
            while (true)
            {
                var c = this.reader.Peek();
                if (c == -1 || c == '\r' || c == '\n')
                    break;

                this.ReadChar();
                sb.Append((char)c);
            }
            this.Value = sb.ToString();
        }

        private string Unescape()
        {
            try
            {
                switch (this.currentChar)
                {
                    case 'b':
                        return "\b";
                    case 't':
                        return "\t";
                    case 'n':
                        return "\n";
                    case 'f':
                        return "\f";
                    case 'r':
                        return "\r";
                    case '"':
                    case '/':
                    case '\\':
                        return ((char)this.currentChar).ToString();
                    case 'u':
                        return char.ConvertFromUtf32(Convert.ToInt32(this.ReadChars(4), 16));
                    case 'U':
                        return char.ConvertFromUtf32(Convert.ToInt32(this.ReadChars(8), 16));
                    default:
                        throw new TomlException(this, "Not a special character.");
                }
            }
            catch (FormatException ex)
            {
                throw new TomlException(this, "Invalid unicode code point.", ex);
            }
        }

        private void ReadBasicString()
        {
            this.ReadChar();
            if (this.currentChar == '"')
            {
                this.ReadMultilineBasicString();
                return;
            }

            this.NodeType = TomlNodeType.BasicString;
            var sb = new StringBuilder();
            while (this.currentChar != '"')
            {
                switch (this.currentChar)
                {
                    case '\r':
                    case '\n':
                        throw new TomlException(this, "A basic string cannot contain the newline characters.");
                    case '\\':
                        this.ReadChar();
                        sb.Append(this.Unescape());
                        break;
                    default:
                        sb.Append((char)this.currentChar);
                        break;
                }
                this.ReadChar();
            }
            this.Value = sb.ToString();
        }

        private static string RemoveFirstNewLine(string s)
        {
            if (s.Length > 0)
            {
                switch (s[0])
                {
                    case '\r':
                        s = s.Substring(s.Length > 1 && s[1] == '\n' ? 2 : 1);
                        break;
                    case '\n':
                        s = s.Substring(1);
                        break;
                }
            }
            return s;
        }

        private void ReadMultilineBasicString()
        {
            this.ReadChar();
            if (this.currentChar != '"')
                throw new TomlException(this, "Unknown string type.");

            this.NodeType = TomlNodeType.MultilineBasicString;
            var sb = new StringBuilder();
            var quoteCount = 0;
            while (quoteCount < 3)
            {
                this.ReadChar();
                if (this.currentChar == -1)
                    throw new TomlException(this, "EOF");

                if (this.currentChar == '"')
                    quoteCount++;
                else
                    quoteCount = 0;

                if (this.currentChar == '\\')
                {
                    this.ReadChar();
                    switch (this.currentChar)
                    {
                        case '\r':
                        case '\n':
                            while (true)
                            {
                                var c = this.reader.Peek();
                                if (c != '\r' && c != '\n' && c != ' ' && c != '\t')
                                    break;
                                this.ReadChar();
                            }
                            break;
                        default:
                            sb.Append(this.Unescape());
                            break;
                    }
                }
                else
                    sb.Append((char)this.currentChar);
            }
            this.Value = RemoveFirstNewLine(sb.ToString(0, sb.Length - 3));
        }

        private void ReadLiteralString()
        {
            this.ReadChar();
            if (this.currentChar == '\'')
            {
                this.ReadMultilineLiteralString();
                return;
            }

            this.NodeType = TomlNodeType.LiteralString;
            var sb = new StringBuilder();
            while (this.currentChar != '\'')
            {
                sb.Append((char)this.currentChar);
                this.ReadChar();
            }
            this.Value = sb.ToString();
        }

        private void ReadMultilineLiteralString()
        {
            this.ReadChar();
            if (this.currentChar != '\'')
                throw new TomlException(this, "Unknown string type.");

            this.NodeType = TomlNodeType.MultilineLiteralString;
            var sb = new StringBuilder();
            var quoteCount = 0;
            while (quoteCount < 3)
            {
                this.ReadChar();
                if (this.currentChar == -1)
                    throw new TomlException(this, "EOF");

                if (this.currentChar == '\'')
                    quoteCount++;
                else
                    quoteCount = 0;

                sb.Append((char)this.currentChar);
            }
            this.Value = RemoveFirstNewLine(sb.ToString(0, sb.Length - 3));
        }

        private void ReadNumber()
        {
            var sb = new StringBuilder();
            sb.Append((char)this.currentChar);
            while (true)
            {
                var i = this.reader.Peek();
                if (i == -1) break;
                var c = (char)i;
                if ("0123456789.+-:TZ".Contains(c))
                {
                    this.ReadChar();
                    sb.Append(c);
                }
                else break;
            }
            var s = sb.ToString();
            if (!s.StartsWith("-") && s.Contains("-"))
            {
                this.NodeType = TomlNodeType.Datetime;
                try
                {
                    this.Value = DateTimeOffset.Parse(s);
                }
                catch (FormatException ex)
                {
                    throw new TomlException(this, "Invalid datetime value.", ex);
                }
            }
            else if (s.Contains("."))
            {
                this.NodeType = TomlNodeType.Float;
                try
                {
                    this.Value = double.Parse(s);
                }
                catch (FormatException ex)
                {
                    throw new TomlException(this, "Invalid float value.", ex);
                }
            }
            else
            {
                this.NodeType = TomlNodeType.Integer;
                try
                {
                    this.Value = long.Parse(s);
                }
                catch (FormatException ex)
                {
                    throw new TomlException(this, "Invalid integer value.", ex);
                }
            }
        }

        private void ReadBoolean()
        {
            this.NodeType = TomlNodeType.Boolean;
            switch ((char)this.currentChar + this.ReadChars(3))
            {
                case "true":
                    this.Value = true;
                    return;
                case "fals":
                    this.ReadChar();
                    if (this.currentChar == 'e')
                    {
                        this.Value = false;
                        return;
                    }
                    break;
            }
            throw new TomlException(this, "Invalid boolean value.");
        }

        private void ReadStartTable()
        {
            this.ReadChar();
            var isArrayOfTable = this.currentChar == '[';
            this.NodeType = isArrayOfTable ? TomlNodeType.StartArrayOfTable : TomlNodeType.StartTable;

            var sb = new StringBuilder();
            if (isArrayOfTable) this.ReadChar();
            while (this.currentChar != ']')
            {
                switch (this.currentChar)
                {
                    case -1:
                        throw new TomlException(this, "EOF");
                    case '\r':
                    case '\n':
                    case ' ':
                    case '\t':
                    case '[':
                        //case '#':
                        throw new TomlException(this, "Invalid table name.");
                }
                sb.Append((char)this.currentChar);
                this.ReadChar();
            }
            if (sb.Length == 0)
                throw new TomlException(this, "The table name is empty.");
            if (isArrayOfTable)
            {
                this.ReadChar();
                if (this.currentChar != ']')
                    throw new TomlException(this, "Invalid the closing delimiter of the array of table.");
            }
            this.Value = sb.ToString();
        }

        private void ReadKey()
        {
            this.NodeType = TomlNodeType.Key;
            var sb = new StringBuilder();
            sb.Append((char)this.currentChar);
            while (true)
            {
                this.ReadChar();
                if (" \t=".Contains((char)this.currentChar)) break;

                switch (this.currentChar)
                {
                    case '\r':
                    case '\n':
                        throw new TomlException(this, "The newline is not expected.");
                    case '#':
                        throw new TomlException(this, "A key cannot contain \"#\" character.");
                }
                sb.Append((char)this.currentChar);
            }
            if (this.currentChar != '=')
            {
                this.MoveNext();
                if (this.currentChar != '=')
                    throw new TomlException(this, "\"=\" is expected.");
            }
            this.Value = sb.ToString();
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                this.disposed = true;
                this.reader.Close();
            }
        }
    }
}
