using System.Text.Json;
using BlueNilePds.Core.CID;
using BlueNilePds.Core.Common;
using PeterO.Cbor;

namespace BlueNilePds.Common.Tests;

public class DagCborLinkTests
{
    private static string SampleCid() => Cid.Create("hello world").ToString();

    [Test]
    public async Task Encode_JsonLinkObject_WritesTag42LinkAsync()
    {
        var cid = SampleCid();
        var json = "{\"ref\":{\"$link\":\"" + cid + "\"}}";

        var bytes = CanonicalDagCbor.Encode(JsonDocument.Parse(json).RootElement);
        var obj = CBORObject.DecodeFromBytes(bytes);

        var link = obj["ref"];
        await Assert.That(link.IsTagged).IsTrue();
        await Assert.That((long)link.GetAllTags()[0]).IsEqualTo(42L);

        var inner = link.Untag();
        await Assert.That(inner.Type).IsEqualTo(CBORType.ByteString);
        await Assert.That(inner.GetByteString()[0]).IsEqualTo((byte)0x00);
        await Assert.That(Cid.FromCBOR(inner).ToString()).IsEqualTo(cid);
    }

    [Test]
    public async Task Encode_JsonLinkObject_IsNotStoredAsMapAsync()
    {
        var cid = SampleCid();
        var json = "{\"ref\":{\"$link\":\"" + cid + "\"}}";

        var bytes = CanonicalDagCbor.Encode(JsonDocument.Parse(json).RootElement);
        var obj = CBORObject.DecodeFromBytes(bytes);

        await Assert.That(obj["ref"].Type).IsNotEqualTo(CBORType.Map);
    }

    [Test]
    public async Task Encode_JsonBytesObject_WritesByteStringAsync()
    {
        var json = """{"data":{"$bytes":"aGVsbG8="}}""";

        var bytes = CanonicalDagCbor.Encode(JsonDocument.Parse(json).RootElement);
        var obj = CBORObject.DecodeFromBytes(bytes);

        await Assert.That(obj["data"].Type).IsEqualTo(CBORType.ByteString);
        await Assert.That(obj["data"].GetByteString().SequenceEqual("hello"u8.ToArray())).IsTrue();
    }

    [Test]
    public async Task Encode_LegacyLinkMap_NormalizesToTag42LinkAsync()
    {
        var cid = SampleCid();
        var legacy = CBORObject.FromJSONString("{\"ref\":{\"$link\":\"" + cid + "\"}}");

        var bytes = CanonicalDagCbor.Encode(legacy);
        var obj = CBORObject.DecodeFromBytes(bytes);

        await Assert.That(obj["ref"].IsTagged).IsTrue();
        await Assert.That(Cid.FromCBOR(obj["ref"].Untag()).ToString()).IsEqualTo(cid);
    }

    [Test]
    public async Task Encode_MalformedLink_ThrowsAsync()
    {
        var json = """{"ref":{"$link":"not-a-cid"}}""";
        var threw = false;
        try
        {
            CanonicalDagCbor.Encode(JsonDocument.Parse(json).RootElement);
        }
        catch (DagCborEncodingException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue();
    }

    [Test]
    public async Task ToJsonElement_Tag42Link_RendersDollarLinkAsync()
    {
        var cid = SampleCid();
        var json = "{\"ref\":{\"$link\":\"" + cid + "\"}}";
        var bytes = CanonicalDagCbor.Encode(JsonDocument.Parse(json).RootElement);

        var element = DagCborJson.ToJsonElement(CBORObject.DecodeFromBytes(bytes));

        await Assert.That(element.GetProperty("ref").GetProperty("$link").GetString()).IsEqualTo(cid);
    }

    [Test]
    public async Task ToJsonElement_ByteString_RendersDollarBytesAsync()
    {
        var json = """{"data":{"$bytes":"aGVsbG8="}}""";
        var bytes = CanonicalDagCbor.Encode(JsonDocument.Parse(json).RootElement);

        var element = DagCborJson.ToJsonElement(CBORObject.DecodeFromBytes(bytes));

        await Assert.That(element.GetProperty("data").GetProperty("$bytes").GetString()).IsEqualTo("aGVsbG8=");
    }

    [Test]
    public async Task ToJsonElement_LegacyLinkMap_StillRendersDollarLinkAsync()
    {
        var cid = SampleCid();
        var legacy = CBORObject.FromJSONString("{\"ref\":{\"$link\":\"" + cid + "\"}}");

        var element = DagCborJson.ToJsonElement(legacy);

        await Assert.That(element.GetProperty("ref").GetProperty("$link").GetString()).IsEqualTo(cid);
    }
}
