using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace HyperTomlProcessor.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var test1 = new XmlTomlReader(new StreamReader("example.toml")))
            using (var test2 = new XmlTomlReader(new StreamReader("hard_example.toml")))
            {
                Console.WriteLine(XElement.Load(test1).ToString());
                Console.WriteLine();
                Console.WriteLine(XElement.Load(test2).ToString());
            }

            Console.ReadKey();
        }
    }
}
