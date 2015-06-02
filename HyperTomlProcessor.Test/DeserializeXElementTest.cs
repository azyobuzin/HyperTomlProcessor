using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HyperTomlProcessor.Test
{
    [TestClass]
    public class DeserializeXElementTest
    {
        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void Error0V03()
        {
            Toml.V03.DeserializeXElement(Examples.Error0);
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void Error0V04()
        {
            Toml.V04.DeserializeXElement(Examples.Error0);
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void Error1V03()
        {
            Toml.V03.DeserializeXElement(Examples.Error1);
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void Error1V04()
        {
            Toml.V04.DeserializeXElement(Examples.Error1);
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void Error2V03()
        {
            Toml.V03.DeserializeXElement(Examples.Error2);
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void Error2V04()
        {
            Toml.V04.DeserializeXElement(Examples.Error2);
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void Error3V03()
        {
            Toml.V03.DeserializeXElement(Examples.Error3);
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void Error3V04()
        {
            Toml.V04.DeserializeXElement(Examples.Error3);
        }

        [TestMethod]
        public void StartsWithV03()
        {
            var toml = Toml.V03;
            toml.DeserializeXElement("#comment\n[TestTable]");
            toml.DeserializeXElement("Test = 1\n[TestTable]");
            toml.DeserializeXElement("[[Test]]\n[[Test]]\n[TestTable]");
            toml.DeserializeXElement("[TestTable]\nTest = 1");
        }

        [TestMethod]
        public void StartsWithV04()
        {
            var toml = Toml.V04;
            toml.DeserializeXElement("#comment\n[TestTable]");
            toml.DeserializeXElement("Test = 1\n[TestTable]");
            toml.DeserializeXElement("[[Test]]\n[[Test]]\n[TestTable]");
            toml.DeserializeXElement("[TestTable]\nTest = 1");
        }

        [TestMethod]
        public void NumberWithUnderscores()
        {
            var dt = DynamicToml.Parse(Toml.V04, "a = 1_2_3_4_5\nb = 9_224_617.445_991\nc = 1e1_00");
            Assert.AreEqual(12345L, dt.a);
            Assert.AreEqual(9224617.445991, dt.b);
            Assert.AreEqual(1e+100, dt.c);
        }

        [TestMethod]
        public void InlineTable()
        {
            var dt = DynamicToml.Parse(Toml.V04, @"
name = { first = ""Tom"", last = ""Preston - Werner"" }
point = { x = 1, y = 2 }
arr = [{ x = 1, y = 2 }, { x = 2, y = 3}]");
            Assert.AreEqual("Tom", dt.name.first);
            Assert.AreEqual("Preston - Werner", dt.name.last);
            Assert.AreEqual(1L, dt.point.x);
            Assert.AreEqual(2L, dt.point.y);
            Assert.AreEqual(1L, dt.arr[0].x);
            Assert.AreEqual(2L, dt.arr[0].y);
            Assert.AreEqual(2L, dt.arr[1].x);
            Assert.AreEqual(3L, dt.arr[1].y);
        }
    }
}
