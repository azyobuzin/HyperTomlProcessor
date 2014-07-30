using System;

namespace HyperTomlProcessor
{
    public class TomlException : Exception
    {
        public TomlException(TomlReader reader, string message, Exception innerException)
            : base(string.Format("{0}\nLine:{1}, Position:{2}", message, reader.LineNumber, reader.LinePosition), innerException)
        {
            this.LineNumber = reader.LineNumber;
            this.LinePosition = reader.LinePosition;
        }

        public TomlException(TomlReader reader, string message) : this(reader, message, null) { }

        public int LineNumber { get; private set; }
        public int LinePosition { get; private set; }
    }
}
