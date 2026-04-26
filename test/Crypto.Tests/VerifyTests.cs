using Crypto.Secp256k1;

namespace Crypto.Tests;

public class VerifyTests
{
    [Test]
    public async Task VerifySignature_SucceedsForValidSignatureAsync()
    {
        var kp = Secp256k1Keypair.Create(exportable: false);
        byte[] data = "test data for signing"u8.ToArray();
        var sig = kp.Sign(data);

        var result = Crypto.Verify.VerifySignature(kp.Did(), data, sig, null, "ES256K");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task VerifySignature_SucceedsWithoutJwtAlgAsync()
    {
        var kp = Secp256k1Keypair.Create(exportable: false);
        byte[] data = "some data"u8.ToArray();
        var sig = kp.Sign(data);

        var result = Crypto.Verify.VerifySignature(kp.Did(), data, sig, null, null);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task VerifySignature_FailsForWrongDataAsync()
    {
        var kp = Secp256k1Keypair.Create(exportable: false);
        byte[] data = "correct data"u8.ToArray();
        var sig = kp.Sign(data);

        byte[] wrongData = "wrong data"u8.ToArray();
        var result = Crypto.Verify.VerifySignature(kp.Did(), wrongData, sig, null, "ES256K");

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task VerifySignature_FailsForWrongJwtAlgAsync()
    {
        var kp = Secp256k1Keypair.Create(exportable: false);
        byte[] data = "test data"u8.ToArray();
        var sig = kp.Sign(data);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            Crypto.Verify.VerifySignature(kp.Did(), data, sig, null, "ES256");
        });
    }

    [Test]
    public async Task VerifySignature_FailsForWrongKeyAsync()
    {
        var kp1 = Secp256k1Keypair.Create(exportable: false);
        var kp2 = Secp256k1Keypair.Create(exportable: false);
        byte[] data = "test data"u8.ToArray();
        var sig = kp1.Sign(data);

        var result = Crypto.Verify.VerifySignature(kp2.Did(), data, sig, null, "ES256K");

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Roundtrip_CreateSignVerifyAsync()
    {
        var kp = Secp256k1Keypair.Create(exportable: true);
        byte[] data = "roundtrip test data"u8.ToArray();
        var sig = kp.Sign(data);

        var did = kp.Did();
        var result = Crypto.Verify.VerifySignature(did, data, sig, null, "ES256K");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Roundtrip_ImportSignVerifyAsync()
    {
        var original = Secp256k1Keypair.Create(exportable: true);
        var privKeyHex = Convert.ToHexString(original.Export()).ToLowerInvariant();

        var imported = Secp256k1Keypair.Import(privKeyHex, exportable: false);
        byte[] data = "imported key test"u8.ToArray();
        var sig = imported.Sign(data);

        var result = Crypto.Verify.VerifySignature(imported.Did(), data, sig, null, "ES256K");

        await Assert.That(result).IsTrue();
    }
}
