using CID;
using Multiformats.Base;
using PeterO.Cbor;

namespace Common.Test;

public class CborTest
{

    [Fact]
    public void TestCbor()
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
        Assert.Equal("Alice", name);
        Assert.Equal(30, age);
        Assert.Equal(cbor, decoded);
        Assert.Equal(obj.Cid, cidObj);
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