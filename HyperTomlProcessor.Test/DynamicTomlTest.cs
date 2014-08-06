using System;
using System.Collections.Generic;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HyperTomlProcessor.Test
{
    [TestClass]
    public class DynamicTomlTest
    {
        [TestMethod]
        public void ParseExample()
        {
            var root = DynamicToml.Parse(Examples.Example);
            Assert.AreEqual("TOML Example", root.title);
            Assert.AreEqual("Tom Preston-Werner", root.owner.name);
            Assert.AreEqual("GitHub", root.owner.organization);
            Assert.AreEqual("GitHub Cofounder & CEO\nLikes tater tots and beer.", root.owner.bio);
            Assert.AreEqual(new DateTimeOffset(1979, 5, 27, 7, 32, 0, TimeSpan.Zero), root.owner.dob);
            Assert.AreEqual("192.168.1.1", root.database.server);
            Utils.SequenceEqual(root.database.ports, 8001L, 8001L, 8002L);
            Assert.AreEqual(5000L, root.database.connection_max);
            Assert.IsTrue(root.database.enabled);
            Assert.AreEqual("10.0.0.1", root.servers.alpha.ip);
            Assert.AreEqual("eqdc10", root.servers.alpha.dc);
            Assert.AreEqual("10.0.0.2", root.servers.beta.ip);
            Assert.AreEqual("eqdc10", root.servers.beta.dc);
            Assert.AreEqual("中国", root.servers.beta.country);
            Utils.SequenceEqual(root.clients.data[0], "gamma", "delta");
            Utils.SequenceEqual(root.clients.data[1], 1L, 2L);
            Utils.SequenceEqual(root.clients.hosts, "alpha", "omega");
            var product0 = root.products[0];
            Assert.AreEqual("Hammer", product0.name);
            Assert.AreEqual(738594937L, product0.sku);
            var product1 = root.products[1];
            Assert.AreEqual("Nail", product1.name);
            Assert.AreEqual(284758393L, product1.sku);
            Assert.AreEqual("gray", product1.color);
        }

        [TestMethod]
        public void ParseHardExample()
        {
            var root = DynamicToml.Parse(Examples.HardExample);
            Assert.AreEqual("You'll hate me after this - #", root.the.test_string);
            Utils.SequenceEqual(root.the.hard.test_array, "] ", " # ");
            Utils.SequenceEqual(root.the.hard.test_array2, "Test #11 ]proved that", "Experiment #9 was a success");
            Assert.AreEqual(" Same thing, but with a string #", root.the.hard.another_test_string);
            Assert.AreEqual(" And when \"'s are in the string, along with # \"", root.the.hard.harder_test_string);
            Assert.AreEqual("You don't think some user won't do that?", root.the.hard["bit#"]["what?"]);
            Utils.SequenceEqual(root.the.hard["bit#"].multi_line_array, "]");
        }

        [TestMethod]
        [ExpectedException(typeof(RuntimeBinderException))]
        public void NotFoundMember()
        {
            var dt = DynamicToml.CreateTable();
            var dummy = dt.foo;
        }

        [TestMethod]
        [ExpectedException(typeof(KeyNotFoundException))]
        public void NotFoundKey()
        {
            var dt = DynamicToml.CreateTable();
            var dummy = dt["foo"];
        }

        [TestMethod]
        public void TableSet()
        {
            var dt = DynamicToml.CreateTable();
            dt.Add("foo", "test");
            Assert.AreEqual("test", dt.foo);
            dt.foo = 1;
            Assert.AreEqual(1L, dt.foo);
            var date = new DateTimeOffset(2014, 8, 6, 21, 8, 5, TimeSpan.FromHours(9));
            dt["foo"] = date;
            Assert.AreEqual(date, dt.foo);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TableAddError()
        {
            var dt = DynamicToml.CreateTable();
            dt.foo = true;
            dt.Add("foo", false);
        }

        [TestMethod]
        public void SerializeAndDeserialize()
        {
            var toml = DynamicToml.Serialize(TestObject.Create());
            TestObject obj = DynamicToml.Parse(toml).Deserialize<TestObject>();
            TestObject.Test(obj);
        }
    }
}
