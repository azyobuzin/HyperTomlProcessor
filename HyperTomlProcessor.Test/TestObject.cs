using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HyperTomlProcessor.Test
{
    [DataContract]
    public class TestObject
    {
        [DataMember]
        public ulong Id { get; set; }
        [DataMember]
        public List<TestObjectContributor> Contributors { get; set; }
        [DataMember]
        public double[] Coordinates { get; set; }
        [DataMember]
        public DateTimeOffset CreatedAt { get; set; }
        [DataMember]
        public bool Favorited { get; set; }
        [DataMember]
        public TestObjectPlace Place { get; set; }
        [DataMember]
        public string Text { get; set; }

        public static TestObject Create()
        {
            return new TestObject
            {
                Id = 114749583439036416,
                Contributors = new List<TestObjectContributor>() {
                    new TestObjectContributor() { Id = 819797, ScreenName = "episod"},
                    new TestObjectContributor() { Id = 98573585, ScreenName = "azyobuzin"}
                },
                Coordinates = new[] { -75.14310264, 40.05701649 },
                CreatedAt = new DateTimeOffset(2008, 8, 27, 13, 8, 45, TimeSpan.Zero),
                Favorited = true,
                Place = new TestObjectPlace()
                {
                    BoudingBox = new[]
                    {
                        new[]
                        {
                            new[] { -77.119759, 38.791645 },
                            new[] { -76.909393, 38.791645 },
                            new[] { -76.909393, 38.995548 },
                            new[] { -77.119759, 38.995548 }
                        },
                        new[]
                        {
                            new[] { 122.933197001144, 24.0456418391239 },
                            new[] { 122.933197001144, 45.5227849999761 },
                            new[] { 145.817458998856, 45.5227849999761 },
                            new[] { 145.817458998856, 24.0456418391239 },
                            new[] { 122.933197001144, 24.0456418391239 }
                        }
                    }
                },
                Text = "Tweet Button, Follow Button, and Web Intents javascript now support SSL http://t.co/9fbA0oYy ^TS\r\n\t突然の日本語"
            };
        }

        public static void Test(TestObject obj)
        {
            Assert.AreEqual(114749583439036416UL, obj.Id);
            Assert.AreEqual(2, obj.Contributors.Count);
            Assert.AreEqual(819797U, obj.Contributors[0].Id);
            Assert.AreEqual("episod", obj.Contributors[0].ScreenName);
            Assert.AreEqual(98573585U, obj.Contributors[1].Id);
            Assert.AreEqual("azyobuzin", obj.Contributors[1].ScreenName);
            obj.Coordinates.SequenceEqual(-75.14310264, 40.05701649);
            Assert.AreEqual(new DateTimeOffset(2008, 8, 27, 13, 8, 45, TimeSpan.Zero), obj.CreatedAt);
            Assert.IsTrue(obj.Favorited);
            Assert.AreEqual(2, obj.Place.BoudingBox.Length);
            Assert.AreEqual(4, obj.Place.BoudingBox[0].Length);
            obj.Place.BoudingBox[0][0].SequenceEqual(-77.119759, 38.791645);
            obj.Place.BoudingBox[0][1].SequenceEqual(-76.909393, 38.791645);
            obj.Place.BoudingBox[0][2].SequenceEqual(-76.909393, 38.995548);
            obj.Place.BoudingBox[0][3].SequenceEqual(-77.119759, 38.995548);
            Assert.AreEqual(5, obj.Place.BoudingBox[1].Length);
            obj.Place.BoudingBox[1][0].SequenceEqual(122.933197001144, 24.0456418391239);
            obj.Place.BoudingBox[1][1].SequenceEqual(122.933197001144, 45.5227849999761);
            obj.Place.BoudingBox[1][2].SequenceEqual(145.817458998856, 45.5227849999761);
            obj.Place.BoudingBox[1][3].SequenceEqual(145.817458998856, 24.0456418391239);
            obj.Place.BoudingBox[1][4].SequenceEqual(122.933197001144, 24.0456418391239);
            Assert.AreEqual(
                "Tweet Button, Follow Button, and Web Intents javascript now support SSL http://t.co/9fbA0oYy ^TS\r\n\t突然の日本語",
                obj.Text
            );
        }
    }

    [DataContract]
    public class TestObjectContributor
    {
        [DataMember]
        public uint Id { get; set; }
        [DataMember]
        public string ScreenName { get; set; }
    }

    [DataContract]
    public class TestObjectPlace
    {
        [DataMember]
        public double[][][] BoudingBox { get; set; }
    }
}
