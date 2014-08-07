using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Xml.Linq;
using Parseq;

namespace HyperTomlProcessor
{
    public static partial class TomlConvert
    {
        public static void SerializeXElement(TextWriter writer, XElement toml)
        {
            XUtils.WriteTo(toml, writer);
        }

        public static void SerializeXElement(Stream stream, XElement toml)
        {
            SerializeXElement(new StreamWriter(stream), toml);
        }

        public static string SerializeXElement(XElement toml)
        {
            return XUtils.GetStreamString(w => SerializeXElement(w, toml));
        }

        // 引数の順番は DataContractJsonSerializer.WriteObject より
        // TODO: .NET4.5 では DataContractJsonSerializerSettings
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

        public static void SerializeObject(Stream stream, object obj, Func<DataContractJsonSerializer> factory = null)
        {
            SerializeObject(new StreamWriter(stream), obj, factory);
        }

        public static string SerializeObject(object obj, Func<DataContractJsonSerializer> factory = null)
        {
            return XUtils.GetStreamString(w => SerializeObject(w, obj, factory));
        }

        public static T DeserializeObject<T>(XElement toml, Func<DataContractJsonSerializer> factory = null)
        {
            using (var xr = toml.CreateReader())
            {
                var s = factory != null ? factory() : new DataContractJsonSerializer(typeof(T));
                return (T)s.ReadObject(xr);
            }
        }

        private static T DeserializeObject<T>(IStream<char> stream, Func<DataContractJsonSerializer> factory = null)
        {
            return DeserializeObject<T>(DeserializeXElement(stream), factory);
        }

        public static T DeserializeObject<T>(TextReader reader, Func<DataContractJsonSerializer> factory = null)
        {
            return DeserializeObject<T>(reader.AsStream(), factory);
        }

        public static T DeserializeObject<T>(Stream stream, Func<DataContractJsonSerializer> factory = null)
        {
            return DeserializeObject<T>(new StreamReader(stream), factory);
        }

        public static T DeserializeObject<T>(IEnumerable<char> toml, Func<DataContractJsonSerializer> factory = null)
        {
            return DeserializeObject<T>(toml.AsStream(), factory);
        }
    }
}
