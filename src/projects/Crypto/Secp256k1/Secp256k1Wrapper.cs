using Secp256k1Net;

namespace Crypto.Secp256k1;

public static class Secp256k1Wrapper
{
    private static readonly Lazy<Secp256k1Net.Secp256k1> Secp256K1 = new(CreateSecp256k1);
    private static readonly object _lock = new();

    private static Secp256k1Net.Secp256k1 CreateSecp256k1()
    {
        Secp256k1NativeLibraryResolver.EnsureLoaded();
        return new Secp256k1Net.Secp256k1();
    }

    public static byte[] CompressPublicKey(byte[] input)
    {
        lock (_lock)
        {
            Secp256k1NativeLibraryResolver.EnsureLoaded();
            return Secp256k1Net.Secp256k1.CompressPublicKey(input);
        }
    }

    public static byte[] DecompressPublicKey(byte[] input)
    {
        lock (_lock)
        {
            Secp256k1NativeLibraryResolver.EnsureLoaded();
            return Secp256k1Net.Secp256k1.DecompressPublicKey(input);
        }
    }

    public static bool SignatureParseCompact(byte[] output, byte[] input)
    {
        lock (_lock)
        {
            return Secp256K1.Value.EcdsaSignatureParseCompact(output, input);
        }
    }

    public static bool Verify(byte[] sig, byte[] msgHash, byte[] publicKey)
    {
        lock (_lock)
        {
            Secp256k1NativeLibraryResolver.EnsureLoaded();
            return Secp256k1Net.Secp256k1.Verify(sig, msgHash, publicKey);
        }
    }

    public static bool SignatureSerializeCompact(byte[] output, byte[] input)
    {
        lock (_lock)
        {
            return Secp256K1.Value.EcdsaSignatureSerializeCompact(output, input);
        }
    }

    public static bool SecretKeyVerify(byte[] secretKey)
    {
        lock (_lock)
        {
            Secp256k1NativeLibraryResolver.EnsureLoaded();
            return Secp256k1Net.Secp256k1.IsValidSecretKey(secretKey);
        }
    }

    public static byte[] PublicKeyCreate(byte[] secretKey)
    {
        lock (_lock)
        {
            Secp256k1NativeLibraryResolver.EnsureLoaded();
            return Secp256k1Net.Secp256k1.CreatePublicKey(secretKey, compressed: false);
        }
    }

    public static byte[] Sign(byte[] msgHash, byte[] secretKey)
    {
        lock (_lock)
        {
            Secp256k1NativeLibraryResolver.EnsureLoaded();
            return Secp256k1Net.Secp256k1.Sign(msgHash, secretKey);
        }
    }
}
