using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HyperTomlProcessor.Test
{
    [TestClass]
    public class DeserializeXElementTest
    {
        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void Error0()
        {
            TomlConvert.DeserializeXElement(Examples.Error0);
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void Error1()
        {
            TomlConvert.DeserializeXElement(Examples.Error1);
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void Error2()
        {
            TomlConvert.DeserializeXElement(Examples.Error2);
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void Error3()
        {
            TomlConvert.DeserializeXElement(Examples.Error3);
        }
    }
}
