namespace Crypto.Secp256k1;

public static class Secp256k1Wrapper
{
    private static Secp256k1Net.Secp256k1 Secp256K1 = new Secp256k1Net.Secp256k1();
    private static object _lock = new object();
    
    /// <inheritdoc cref="Secp256k1Net.Secp256k1.PublicKeyParse"/>
    public static bool PublicKeyParse(byte[] output, byte[] input)
    {
        lock (_lock)
        {
            return Secp256K1.PublicKeyParse(output, input);
        }
    }
    
    /// <inheritdoc cref="Secp256k1Net.Secp256k1.SignatureParseCompact"/>
    public static bool SignatureParseCompact(byte[] output, byte[] input)
    {
        lock (_lock)
        {
            return Secp256K1.SignatureParseCompact(output, input);
        }
    }
    
    /// <inheritdoc cref="Secp256k1Net.Secp256k1.Verify"/>
    public static bool Verify(byte[] sig, byte[] msgHash, byte[] publicKey)
    {
        lock (_lock)
        {
            return Secp256K1.Verify(sig, msgHash, publicKey);
        }
    }
    
    /// <inheritdoc cref="Secp256k1Net.Secp256k1.SignatureSerializeCompact"/>
    public static bool SignatureSerializeCompact(byte[] output, byte[] input)
    {
        lock (_lock)
        {
            return Secp256K1.SignatureSerializeCompact(output, input);
        }
    }
    
    /// <inheritdoc cref="Secp256k1Net.Secp256k1.PublicKeySerialize"/>
    public static bool PublicKeySerialize(byte[] output, byte[] input, Secp256k1Net.Flags flags)
    {
        lock (_lock)
        {
            return Secp256K1.PublicKeySerialize(output, input, flags);
        }
    }
    
    /// <inheritdoc cref="Secp256k1Net.Secp256k1.SecretKeyVerify"/>
    public static bool SecretKeyVerify(byte[] secretKey)
    {
        lock (_lock)
        {
            return Secp256K1.SecretKeyVerify(secretKey);
        }
    }
    
    /// <inheritdoc cref="Secp256k1Net.Secp256k1.PublicKeyCreate"/>
    public static bool PublicKeyCreate(byte[] publicKey, byte[] secretKey)
    {
        lock (_lock)
        {
            return Secp256K1.PublicKeyCreate(publicKey, secretKey);
        }
    }
    
    /// <inheritdoc cref="Secp256k1Net.Secp256k1.Sign"/>
    public static bool Sign(byte[] signature, byte[] msgHash, byte[] secretKey)
    {
        lock (_lock)
        {
            return Secp256K1.Sign(signature, msgHash, secretKey);
        }
    }
}