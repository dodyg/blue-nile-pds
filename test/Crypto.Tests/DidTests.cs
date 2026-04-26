using Crypto.Secp256k1;

namespace Crypto.Tests;

public class DidTests
{
    [Test]
    public async Task FormatDidKey_StartsWithDidKeyPrefixAsync()
    {
        var kp = Secp256k1Keypair.Create(exportable: false);
        var did = kp.Did();

        await Assert.That(did).StartsWith("did:key:");
    }

    [Test]
    public async Task ParseDidKey_RoundtripAsync()
    {
        var kp = Secp256k1Keypair.Create(exportable: false);
        var did = kp.Did();

        var parsed = Crypto.Did.ParseDidKey(did);

        await Assert.That(parsed.JwtAlg).IsEqualTo("ES256K");
        await Assert.That(parsed.KeyBytes).IsNotNull();
        await Assert.That(parsed.KeyBytes.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task FormatDidKey_ProducesConsistentResultAsync()
    {
        var kp = Secp256k1Keypair.Create(exportable: false);

        var did1 = kp.Did();
        var did2 = kp.Did();

        await Assert.That(did1).IsEqualTo(did2);
    }

    [Test]
    public async Task ParseDidKey_ThrowsForInvalidPrefixAsync()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            Crypto.Did.ParseDidKey("not:a:valid:did");
        });
    }

    [Test]
    public async Task ParseDidKey_ThrowsForEmptyStringAsync()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            Crypto.Did.ParseDidKey("");
        });
    }

    [Test]
    public async Task FormatMultiKey_StartsWithBase58PrefixAsync()
    {
        var kp = Secp256k1Keypair.Create(exportable: false);
        var did = kp.Did();
        var multiKey = did["did:key:".Length..];

        await Assert.That(multiKey).StartsWith("z");
    }

    [Test]
    public async Task ExtractMultiKey_ExtractsCorrectlyAsync()
    {
        var kp = Secp256k1Keypair.Create(exportable: false);
        var did = kp.Did();

        var multiKey = Utils.ExtractMultiKey(did);

        await Assert.That(multiKey).StartsWith("z");
        await Assert.That(did).IsEqualTo($"did:key:{multiKey}");
    }

    [Test]
    public async Task ParseDidKey_ProducesValidKeyBytesForImportedKeyAsync()
    {
        var kp = Secp256k1Keypair.Create(exportable: true);
        var did = kp.Did();
        var parsed = Crypto.Did.ParseDidKey(did);

        byte[] data = "verification test"u8.ToArray();
        var sig = kp.Sign(data);
        var result = Crypto.Verify.VerifySignature(did, data, sig, null, "ES256K");

        await Assert.That(result).IsTrue();
    }
}
