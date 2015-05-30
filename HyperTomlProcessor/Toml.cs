using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Xml.Linq;
using Parseq;

namespace HyperTomlProcessor
{
    /// <summary>
    /// Provides methods for converting between <see cref="XElement"/> and TOML.
    /// </summary>
    public class Toml
    {
        //TODO: バージョン別書き出し
        internal Toml(Parser<char, TomlParser.ParseResult> parser)
        {
            this.parser = parser;
        }

        private Parser<char, TomlParser.ParseResult> parser;

        private static Toml v03;

        /// <summary>
        /// Gets the <see cref="Toml"/> instance for TOML v0.3.0.
        /// </summary>
        public static Toml V03
        {
            get
            {
                if (v03 == null)
                    v03 = new Toml(TomlParser.V03Parser);
                return v03;
            }
        }

        private static Toml v04;

        /// <summary>
        /// Gets the <see cref="Toml"/> instance for TOML v0.4.0.
        /// </summary>
        public static Toml V04
        {
            get
            {
                if (v04 == null)
                    v04 = new Toml(TomlParser.V04Parser);
                return v04;
            }
        }

        /// <summary>
        /// Deserializes the TOML to an <see cref="XElement"/>.
        /// </summary>
        /// <param name="toml">The TOML string to deserialize.</param>
        /// <returns>The deserialized <see cref="XElement"/>.</returns>
        public XElement DeserializeXElement(IEnumerable<char> toml)
        {
            return this.parser.DeserializeXElement(toml.AsStream());
        }

        /// <summary>
        /// Deserializes the TOML to an <see cref="XElement"/>.
        /// </summary>
        /// <param name="reader">The <see cref="TextReader"/> that contains the TOML to deserialize.</param>
        /// <returns>The deserialized <see cref="XElement"/>.</returns>
        public XElement DeserializeXElement(TextReader reader)
        {
            return this.parser.DeserializeXElement(reader.AsStream());
        }

        /// <summary>
        /// Deserializes the TOML to an <see cref="XElement"/>.
        /// </summary>
        /// <param name="stream">The stream that contains the TOML to deserialize.</param>
        /// <returns>The deserialized <see cref="XElement"/>.</returns>
        public XElement DeserializeXElement(Stream stream)
        {
            return this.DeserializeXElement(new StreamReader(stream));
        }

        /// <summary>
        /// Serializes the <see cref="XElement"/> to TOML.
        /// </summary>
        /// <param name="writer">A <see cref="TextWriter"/> to write the TOML content to.</param>
        /// <param name="toml">The <see cref="XElement"/> to convert to TOML.</param>
        public static void SerializeXElement(TextWriter writer, XElement toml)
        {
            XUtils.WriteTo(toml, writer);
        }

        /// <summary>
        /// Serializes the <see cref="XElement"/> to TOML.
        /// </summary>
        /// <param name="stream">A stream to write the TOML content to.</param>
        /// <param name="toml">The <see cref="XElement"/> to convert to TOML.</param>
        public static void SerializeXElement(Stream stream, XElement toml)
        {
            SerializeXElement(new StreamWriter(stream), toml);
        }

        /// <summary>
        /// Serializes the <see cref="XElement"/> to a TOML string.
        /// </summary>
        /// <param name="toml">The <see cref="XElement"/> to convert to TOML.</param>
        /// <returns>A TOML string.</returns>
        public static string SerializeXElement(XElement toml)
        {
            return XUtils.GetStreamString(w => SerializeXElement(w, toml));
        }

        // 引数の順番は DataContractJsonSerializer.WriteObject より
        /// <summary>
        /// Serializes the specified object to TOML.
        /// </summary>
        /// <param name="writer">A <see cref="TextWriter"/> to write the TOML content to.</param>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="factory">
        /// The function to make a <see cref="DataContractJsonSerializer"/>.
        /// if null, it will make a <see cref="DataContractJsonSerializer"/> with default settings.
        /// </param>
        public static void SerializeObject(TextWriter writer, object obj, Func<DataContractJsonSerializer> factory = null)
        {
            var xd = new XDocument();
            using (var xw = xd.CreateWriter())
            {
                var s = factory != null ? factory() : new DataContractJsonSerializer(obj.GetType());
                s.WriteObject(xw, obj);
            }
            SerializeXElement(writer, xd.Root);
        }

        /// <summary>
        /// Serializes the specified object to TOML.
        /// </summary>
        /// <param name="stream">A stream to write the TOML content to.</param>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="factory">
        /// The function to make a <see cref="DataContractJsonSerializer"/>.
        /// if null, it will make a <see cref="DataContractJsonSerializer"/> with default settings.
        /// </param>
        public static void SerializeObject(Stream stream, object obj, Func<DataContractJsonSerializer> factory = null)
        {
            SerializeObject(new StreamWriter(stream), obj, factory);
        }

        /// <summary>
        /// Serializes the specified object to a TOML string.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="factory">
        /// The function to make a <see cref="DataContractJsonSerializer"/>.
        /// if null, it will make a <see cref="DataContractJsonSerializer"/> with default settings.
        /// </param>
        /// <returns>A TOML string.</returns>
        public static string SerializeObject(object obj, Func<DataContractJsonSerializer> factory = null)
        {
            return XUtils.GetStreamString(w => SerializeObject(w, obj, factory));
        }

#if NET45
        private static Func<DataContractJsonSerializer> MakeFactory(Type type, DataContractJsonSerializerSettings settings)
        {
            return () => new DataContractJsonSerializer(type, settings);
        }

        /// <summary>
        /// Serializes the specified object to TOML.
        /// </summary>
        /// <param name="writer">A <see cref="TextWriter"/> to write the TOML content to.</param>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="settings">A <see cref="DataContractJsonSerializerSettings"/> to make the <see cref="DataContractJsonSerializer"/>.</param>
        public static void SerializeObject(TextWriter writer, object obj, DataContractJsonSerializerSettings settings)
        {
            SerializeObject(writer, obj, MakeFactory(obj.GetType(), settings));
        }

        /// <summary>
        /// Serializes the specified object to TOML.
        /// </summary>
        /// <param name="stream">A stream to write the TOML content to.</param>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="settings">A <see cref="DataContractJsonSerializerSettings"/> to make the <see cref="DataContractJsonSerializer"/>.</param>
        public static void SerializeObject(Stream stream, object obj, DataContractJsonSerializerSettings settings)
        {
            SerializeObject(stream, obj, MakeFactory(obj.GetType(), settings));
        }

        /// <summary>
        /// Serializes the specified object to a TOML string.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="settings">A <see cref="DataContractJsonSerializerSettings"/> to make the <see cref="DataContractJsonSerializer"/>.</param>
        /// <returns>A TOML string.</returns>
        public static string SerializeObject(object obj, DataContractJsonSerializerSettings settings)
        {
            return SerializeObject(obj, MakeFactory(obj.GetType(), settings));
        }
#endif

        /// <summary>
        /// Deserializes the TOML to the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
        /// <param name="toml">The <see cref="XElement"/> that represents the TOML structure to deserialize.</param>
        /// <param name="factory">
        /// The function to make a <see cref="DataContractJsonSerializer"/>.
        /// if null, it will make a <see cref="DataContractJsonSerializer"/> with default settings.
        /// </param>
        /// <returns>The deserialized object.</returns>
        public static T DeserializeObject<T>(XElement toml, Func<DataContractJsonSerializer> factory = null)
        {
            using (var xr = toml.CreateReader())
            {
                var s = factory != null ? factory() : new DataContractJsonSerializer(typeof(T));
                return (T)s.ReadObject(xr);
            }
        }

        private T DeserializeObject<T>(ITokenStream<char> stream, Func<DataContractJsonSerializer> factory = null)
        {
            return DeserializeObject<T>(this.parser.DeserializeXElement(stream), factory);
        }

        /// <summary>
        /// Deserializes the TOML to the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
        /// <param name="reader">The <see cref="TextReader"/> that contains the TOML to deserialize.</param>
        /// <param name="factory">
        /// The function to make a <see cref="DataContractJsonSerializer"/>.
        /// if null, it will make a <see cref="DataContractJsonSerializer"/> with default settings.
        /// </param>
        /// <returns>The deserialized object.</returns>
        public T DeserializeObject<T>(TextReader reader, Func<DataContractJsonSerializer> factory = null)
        {
            return this.DeserializeObject<T>(reader.AsStream(), factory);
        }

        /// <summary>
        /// Deserializes the TOML to the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
        /// <param name="stream">The stream that contains the TOML to deserialize.</param>
        /// <param name="factory">
        /// The function to make a <see cref="DataContractJsonSerializer"/>.
        /// if null, it will make a <see cref="DataContractJsonSerializer"/> with default settings.
        /// </param>
        /// <returns>The deserialized object.</returns>
        public T DeserializeObject<T>(Stream stream, Func<DataContractJsonSerializer> factory = null)
        {
            return this.DeserializeObject<T>(new StreamReader(stream), factory);
        }

        /// <summary>
        /// Deserializes the TOML to the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
        /// <param name="toml">The TOML string to deserialize.</param>
        /// <param name="factory">
        /// The function to make a <see cref="DataContractJsonSerializer"/>.
        /// if null, it will make a <see cref="DataContractJsonSerializer"/> with default settings.
        /// </param>
        /// <returns>The deserialized object.</returns>
        public T DeserializeObject<T>(IEnumerable<char> toml, Func<DataContractJsonSerializer> factory = null)
        {
            return this.DeserializeObject<T>(toml.AsStream(), factory);
        }

#if NET45
        /// <summary>
        /// Deserializes the TOML to the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
        /// <param name="toml">The <see cref="XElement"/> that represents the TOML structure to deserialize.</param>
        /// <param name="settings">A <see cref="DataContractJsonSerializerSettings"/> to make the <see cref="DataContractJsonSerializer"/>.</param>
        /// <returns>The deserialized object.</returns>
        public static T DeserializeObject<T>(XElement toml, DataContractJsonSerializerSettings settings)
        {
            return DeserializeObject<T>(toml, MakeFactory(typeof(T), settings));
        }

        /// <summary>
        /// Deserializes the TOML to the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
        /// <param name="reader">The <see cref="TextReader"/> that contains the TOML to deserialize.</param>
        /// <param name="settings">A <see cref="DataContractJsonSerializerSettings"/> to make the <see cref="DataContractJsonSerializer"/>.</param>
        /// <returns>The deserialized object.</returns>
        public T DeserializeObject<T>(TextReader reader, DataContractJsonSerializerSettings settings)
        {
            return this.DeserializeObject<T>(reader, MakeFactory(typeof(T), settings));
        }

        /// <summary>
        /// Deserializes the TOML to the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
        /// <param name="stream">The stream that contains the TOML to deserialize.</param>
        /// <param name="settings">A <see cref="DataContractJsonSerializerSettings"/> to make the <see cref="DataContractJsonSerializer"/>.</param>
        /// <returns>The deserialized object.</returns>
        public T DeserializeObject<T>(Stream stream, DataContractJsonSerializerSettings settings)
        {
            return this.DeserializeObject<T>(stream, MakeFactory(typeof(T), settings));
        }

        /// <summary>
        /// Deserializes the TOML to the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
        /// <param name="toml">The TOML string to deserialize.</param>
        /// <param name="settings">A <see cref="DataContractJsonSerializerSettings"/> to make the <see cref="DataContractJsonSerializer"/>.</param>
        /// <returns>The deserialized object.</returns>
        public T DeserializeObject<T>(IEnumerable<char> toml, DataContractJsonSerializerSettings settings)
        {
            return this.DeserializeObject<T>(toml, MakeFactory(typeof(T), settings));
        }
#endif
    }
}
