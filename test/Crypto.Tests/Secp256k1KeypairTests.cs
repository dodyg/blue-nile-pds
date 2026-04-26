using Crypto.Secp256k1;

namespace Crypto.Tests;

public class Secp256k1KeypairTests
{
    [Test]
    public async Task Create_ReturnsKeyPairWithDidAsync()
    {
        var kp = Secp256k1Keypair.Create(exportable: true);

        await Assert.That(kp.JwtAlg).IsEqualTo("ES256K");
        await Assert.That(kp.Did()).StartsWith("did:key:");
    }

    [Test]
    public async Task Create_DidStartsWithCorrectPrefixAsync()
    {
        var kp = Secp256k1Keypair.Create(exportable: false);
        var did = kp.Did();

        await Assert.That(did).StartsWith("did:key:z");
    }

    [Test]
    public async Task Export_ReturnsPrivateKeyWhenExportableAsync()
    {
        var kp = Secp256k1Keypair.Create(exportable: true);
        var exported = kp.Export();

        await Assert.That(exported).IsNotNull();
        await Assert.That(exported.Length).IsEqualTo(32);
    }

    [Test]
    public async Task Export_ThrowsWhenNotExportableAsync()
    {
        var kp = Secp256k1Keypair.Create(exportable: false);

        await Assert.ThrowsAsync<Exception>(async () =>
        {
            kp.Export();
        });
    }

    [Test]
    public async Task Import_FromHexStringAsync()
    {
        var original = Secp256k1Keypair.Create(exportable: true);
        var hexPrivKey = Convert.ToHexString(original.Export()).ToLowerInvariant();

        var imported = Secp256k1Keypair.Import(hexPrivKey, exportable: true);

        await Assert.That(imported.Did()).IsEqualTo(original.Did());
    }

    [Test]
    public async Task Import_FromBytesAsync()
    {
        var original = Secp256k1Keypair.Create(exportable: true);
        var privKeyBytes = original.Export();

        var imported = Secp256k1Keypair.Import(privKeyBytes, exportable: true);

        await Assert.That(imported.Did()).IsEqualTo(original.Did());
        var exportedBytes = imported.Export();
        await Assert.That(exportedBytes).IsEqualTo(privKeyBytes);
    }

    [Test]
    public async Task Sign_ReturnsNonNullSignatureAsync()
    {
        var kp = Secp256k1Keypair.Create(exportable: false);
        byte[] data = "test message"u8.ToArray();

        var sig = kp.Sign(data);

        await Assert.That(sig).IsNotNull();
        await Assert.That(sig.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Sign_ProducesCompactSignatureAsync()
    {
        var kp = Secp256k1Keypair.Create(exportable: false);
        byte[] data = "hello world"u8.ToArray();

        var sig = kp.Sign(data);

        await Assert.That(sig.Length).IsEqualTo(64);
    }

    [Test]
    public async Task Did_IsDeterministicAsync()
    {
        var kp = Secp256k1Keypair.Create(exportable: true);
        var did1 = kp.Did();
        var did2 = kp.Did();

        await Assert.That(did1).IsEqualTo(did2);
    }

    [Test]
    public async Task Constructor_RejectsInvalidPrivateKeyLengthAsync()
    {
        byte[] badKey = new byte[16];

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            new Secp256k1Keypair(badKey, exportable: true);
        });
    }

    [Test]
    public async Task DifferentKeyPairs_ProduceDifferentDidsAsync()
    {
        var kp1 = Secp256k1Keypair.Create(exportable: false);
        var kp2 = Secp256k1Keypair.Create(exportable: false);

        await Assert.That(kp1.Did()).IsNotEqualTo(kp2.Did());
    }
}
