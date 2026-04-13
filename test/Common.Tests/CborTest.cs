using CID;
using Multiformats.Base;
using PeterO.Cbor;
using System.Threading.Tasks;

namespace Common.Tests;

public class CborTest
{

    [Test]
    public async Task TestCborAsync()
    {
        var obj = new TestObject("Alice", 30, Cid.Create("hello world", MultibaseEncoding.Base32Upper));
        var cbor = CBORObject.NewMap()
            .Add("Name", obj.Name)
            .Add("Age", obj.Age)
            .Add("Cid", obj.Cid.ToString());
        var block = CborBlock.Encode(cbor);
        var decoded = CborBlock.Decode(block.Bytes);
        var name = decoded["Name"].AsString();
        var age = decoded["Age"].AsInt32();
        var cid = decoded["Cid"].AsString();
        var cidObj = Cid.FromString(cid);
        await Assert.That(name).IsEqualTo("Alice");
        await Assert.That(age).IsEqualTo(30);
        await Assert.That(decoded).IsEqualTo(cbor);
        await Assert.That(cidObj).IsEqualTo(obj.Cid);
    }

    public class TestObject
    {
        public TestObject(string name, int age, Cid cid)
        {
            Name = name;
            Age = age;
            Cid = cid;
        }

        public string Name { get; }
        public int Age { get; }

        public Cid Cid { get; }
    }
}