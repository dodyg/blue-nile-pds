using System.IO;
using Ipfs;
using Multiformats.Base;
using Multiformats.Codec;
using System.Threading.Tasks;

namespace CID.Tests;

public class CidTests
{
    [Test]
    [Arguments("hello world", MultibaseEncoding.Base58Btc, "zb2rhj7crUKTQYRGCRATFaQ6YFLTde2YzdqbbhAASkL9uRDXn")]
    [Arguments("hello world", MultibaseEncoding.Base32Upper, "BAFKREIFZJUT3TE2NHYEKKLSS27NH3K72YSCO7Y32KOAO5EEI66WOF36N5E")]
    [Arguments("foo", MultibaseEncoding.Base32Lower, "bafkreibme22gw2h7y2h7tg2fhqotaqjucnbc24deqo72b6mkl2egezxhvy")]
    [Arguments("", MultibaseEncoding.Base32Upper, "BAFKREIHDWDCEFGH4DQKJV67UZCMW7OJEE6XEDZDETOJUZJEVTENXQUVYKU")]
    [Arguments("", MultibaseEncoding.Base58Btc, "zb2rhmy65F3REf8SZp7De11gxtECBGgUKaLdiDj7MCGCHxbDW")]
    [Arguments("foo", MultibaseEncoding.Base64, "mAVUSICwmtGto/8aP+ZtFPB0wQTQTQi1wZIO/oPmKXohiZueu")]
    public async Task CreateCidAsync(string input, MultibaseEncoding encoding, string expectedOutput)
    {
        var cid = Cid.Create(input);
        var outputStr = cid.ToStringOfBase(encoding);

        await Assert.That(outputStr).IsEqualTo(expectedOutput);
    }

    [Test]
    [Arguments("foo", MultibaseEncoding.Base58Btc, "QmRJzsvyCQyizr73Gmms8ZRtvNxmgqumxc2KUp71dfEmoj")]
    public async Task CreateCidV0Async(string input, MultibaseEncoding encoding, string expectedOutput)
    {
        var digest = Util.Sha2_256Digest(input);
        var cid = Cid.NewV0(digest);
        var outputStr = cid.ToStringOfBase(encoding);

        await Assert.That(outputStr).IsEqualTo(expectedOutput);
    }

    [Test]
    public async Task BasicMarshallingAsync()
    {
        var cid = Cid.NewV1(Cid.DAG_PB, Util.Sha2_256Digest("beep boop"), MultibaseEncoding.Base32Lower);
        var data = cid.ToBytes();
        var cmp = Cid.ReadBytes(data, MultibaseEncoding.Base32Lower);
        await Assert.That(cmp).IsEqualTo(cid);

        var cmp2 = cmp.ToV1();
        await Assert.That(cmp2).IsEqualTo(cid);

        var s = cid.ToString();
        var cmp3 = Cid.FromString(s);
        await Assert.That(cmp3).IsEqualTo(cid);
    }

    [Test]
    public async Task FromStringTestAsync()
    {
        var input = "bafkreibme22gw2h7y2h7tg2fhqotaqjucnbc24deqo72b6mkl2egezxhvy";
        var expectedOutput = Util.Sha2_256Digest("foo");
        var cid = Cid.FromString(input);

        await Assert.That(cid.Version).IsEqualTo(Version.V1);
        await Assert.That(cid.Codec).IsEqualTo((ulong)MulticodecCode.Raw);
        await Assert.That(cid.MultiHash).IsEqualTo(expectedOutput);
    }

    [Test]
    public async Task FromStringTest2Async()
    {
        var input = "zUFKqwZsvwnjhQeVttU28NEx8z4mfJJN7U4KfG8rFVAoXHKF2";
        var cid = Cid.FromString(input);
        var cid2 = Ipfs.Cid.Decode(input);

        await Assert.That(cid.Version).IsEqualTo(Version.V1);
    }

    [Test]
    public async Task V0HandlingAsync()
    {
        var old = "QmdfTbBqBPQ7VNxZEYEj14VmRuZBkqFbiwReogJgS1zR1n";
        var cid = Cid.FromString(old);

        await Assert.That(cid.Version).IsEqualTo(Version.V0);
        await Assert.That(cid.ToStringOfBase(MultibaseEncoding.Base58Btc)).IsEqualTo(old);
    }

    [Test]
    public void V0Error()
    {
        // TODO: this should throw an error?
        //var bad = "QmdfTbBqBPQ7VNxZEYEj14VmRuZBkqFbiwReogJgS1zIII";
    }

    [Test]
    public async Task ValidateErrorAsync()
    {
        byte[] badCid = [255, 255, 255, 255, 0, 6, 85, 0];
        await Assert.ThrowsAsync<CIDException>(() => Task.Run(() => Cid.ReadBytes(badCid)));
    }

    [Test]
    public async Task ExplicitV0IsDisallowedAsync()
    {
        byte[] data = [0x00, 0x70, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12];
        await Assert.ThrowsAsync<CIDException>(() => Task.Run(() => Cid.ReadBytes(data)));
    }

    [Test]
    public async Task ToStringOfBaseV0ErrorAsync()
    {
        var digest = Util.Sha2_256Digest("foo");
        var cid = Cid.NewV0(digest);
        await Assert.ThrowsAsync<CIDException>(() => Task.Run(() => cid.ToStringOfBase(MultibaseEncoding.Base16Upper)));
    }

    [Test]
    public async Task TestHashAsync()
    {
        byte[] data = [1, 2, 3];
        var hash = Util.Sha2_256Digest(data);
        var cid = Cid.NewV0(hash);

        // test that we can store the CID as a hash and get it back
        var dict = new Dictionary<Cid, byte[]>
        {
            [cid] = data
        };

        await Assert.That(dict.ContainsKey(cid)).IsTrue();
        await Assert.That(dict[cid]).IsEqualTo(data);
    }

    [Test]
    [Arguments("hello-world.png", "b487017b538407049156bb2609702277a3574ad7a8cee6d7017903085aad3d11")]
    public async Task TestCidForBlobAsync(string blobFileName, string actualDigest)
    {
        var filePath = @"./data/blobs" + "/" + blobFileName;
        var stream = File.OpenRead(filePath);

        var result = await Util.CidForBlobsAsync(stream);

        await Assert.That(result.Codec).IsEqualTo((ulong)MulticodecCode.Raw);
        await Assert.That(result.Version).IsEqualTo(Version.V1);
        await Assert.That(actualDigest).IsEqualTo(result.MultiHash.Digest.ToHexString());
    }
}
