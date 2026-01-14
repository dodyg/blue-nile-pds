using Ipfs;
using Multiformats.Base;
using Multiformats.Codec;

namespace CID.Tests;

public class CidTests
{
    [Theory]
    [InlineData("hello world", MultibaseEncoding.Base58Btc, "zb2rhj7crUKTQYRGCRATFaQ6YFLTde2YzdqbbhAASkL9uRDXn")]
    [InlineData("hello world", MultibaseEncoding.Base32Upper, "BAFKREIFZJUT3TE2NHYEKKLSS27NH3K72YSCO7Y32KOAO5EEI66WOF36N5E")]
    [InlineData("foo", MultibaseEncoding.Base32Lower, "bafkreibme22gw2h7y2h7tg2fhqotaqjucnbc24deqo72b6mkl2egezxhvy")]
    [InlineData("", MultibaseEncoding.Base32Upper, "BAFKREIHDWDCEFGH4DQKJV67UZCMW7OJEE6XEDZDETOJUZJEVTENXQUVYKU")]
    [InlineData("", MultibaseEncoding.Base58Btc, "zb2rhmy65F3REf8SZp7De11gxtECBGgUKaLdiDj7MCGCHxbDW")]
    [InlineData("foo", MultibaseEncoding.Base64, "mAVUSICwmtGto/8aP+ZtFPB0wQTQTQi1wZIO/oPmKXohiZueu")]
    public void CreateCid(string input, MultibaseEncoding encoding, string expectedOutput)
    {
        var cid = Cid.Create(input);
        var outputStr = cid.ToStringOfBase(encoding);

        Assert.Equal(expectedOutput, outputStr);
    }

    [Theory]
    [InlineData("foo", MultibaseEncoding.Base58Btc, "QmRJzsvyCQyizr73Gmms8ZRtvNxmgqumxc2KUp71dfEmoj")]
    public void CreateCidV0(string input, MultibaseEncoding encoding, string expectedOutput)
    {
        var digest = Util.Sha2_256Digest(input);
        var cid = Cid.NewV0(digest);
        var outputStr = cid.ToStringOfBase(encoding);

        Assert.Equal(expectedOutput, outputStr);
    }

    [Fact]
    public void BasicMarshalling()
    {
        var cid = Cid.NewV1(Cid.DAG_PB, Util.Sha2_256Digest("beep boop"), MultibaseEncoding.Base32Lower);
        var data = cid.ToBytes();
        var cmp = Cid.ReadBytes(data, MultibaseEncoding.Base32Lower);
        Assert.Equal(cid, cmp);

        var cmp2 = cmp.ToV1();
        Assert.Equal(cid, cmp2);

        var s = cid.ToString();
        var cmp3 = Cid.FromString(s);
        Assert.Equal(cid, cmp3);
    }

    [Fact]
    public void FromStringTest()
    {
        var input = "bafkreibme22gw2h7y2h7tg2fhqotaqjucnbc24deqo72b6mkl2egezxhvy";
        var expectedOutput = Util.Sha2_256Digest("foo");
        var cid = Cid.FromString(input);

        Assert.Equal(Version.V1, cid.Version);
        Assert.Equal((ulong)MulticodecCode.Raw, cid.Codec);
        Assert.Equal(expectedOutput, cid.MultiHash);
    }

    [Fact]
    public void FromStringTest2()
    {
        var input = "zUFKqwZsvwnjhQeVttU28NEx8z4mfJJN7U4KfG8rFVAoXHKF2";
        var cid = Cid.FromString(input);
        var cid2 = Ipfs.Cid.Decode(input);

        Assert.Equal(Version.V1, cid.Version);
    }

    [Fact]
    public void V0Handling()
    {
        var old = "QmdfTbBqBPQ7VNxZEYEj14VmRuZBkqFbiwReogJgS1zR1n";
        var cid = Cid.FromString(old);

        Assert.Equal(Version.V0, cid.Version);
        Assert.Equal(old, cid.ToStringOfBase(MultibaseEncoding.Base58Btc));
    }

    [Fact]
    public void V0Error()
    {
        // TODO: this should throw an error?
        //var bad = "QmdfTbBqBPQ7VNxZEYEj14VmRuZBkqFbiwReogJgS1zIII";
        //Assert.Throws<CIDException>(() => Cid.FromString(bad));
    }

    [Fact]
    public void ValidateError()
    {
        byte[] badCid = [255, 255, 255, 255, 0, 6, 85, 0];
        Assert.Throws<CIDException>(() => Cid.ReadBytes(badCid));
    }

    [Fact]
    public void ExplicitV0IsDisallowed()
    {
        byte[] data = [0x00, 0x70, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12];
        Assert.Throws<CIDException>(() => Cid.ReadBytes(data));
    }

    [Fact]
    public void ToStringOfBaseV0Error()
    {
        var digest = Util.Sha2_256Digest("foo");
        var cid = Cid.NewV0(digest);
        Assert.Throws<CIDException>(() => cid.ToStringOfBase(MultibaseEncoding.Base16Upper));
    }

    [Fact]
    public void TestHash()
    {
        byte[] data = [1, 2, 3];
        var hash = Util.Sha2_256Digest(data);
        var cid = Cid.NewV0(hash);

        // test that we can store the CID as a hash and get it back
        var dict = new Dictionary<Cid, byte[]>
        {
            [cid] = data
        };

        Assert.True(dict.ContainsKey(cid));
        Assert.Equal(data, dict[cid]);
    }

    [Theory]
    [InlineData("hello-world.png", "b487017b538407049156bb2609702277a3574ad7a8cee6d7017903085aad3d11")]
    public async Task TestCidForBlob(string blobFileName, string actualDigest)
    {
        var filePath = @"./data/blobs" + "/" + blobFileName;
        var stream = File.OpenRead(filePath);

        var result = await Util.CidForBlobs(stream);

        Assert.Equal((ulong)MulticodecCode.Raw, result.Codec);
        Assert.Equal(Version.V1, result.Version);
        Assert.Equal(result.MultiHash.Digest.ToHexString(), actualDigest);
    }
}