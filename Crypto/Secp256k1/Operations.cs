using System.Security.Cryptography;

namespace Crypto.Secp256k1;

public class Operations
{
    private static readonly Secp256k1Net.Secp256k1 Secp256K1 = new();
    
    public static bool VerifyDidSig(string did, byte[] data, byte[] sig, VerifyOptions? opts)
    {
        var prefixedBytes = Utils.ExtractPrefixedBytes(Utils.ExtractMultiKey(did));
        if (!Utils.HasPrefix(prefixedBytes, Const.SECP256K1_DID_PREFIX))
        {
            throw new ArgumentException($"Not a secp256k1 did:key: {did}");
        }
        
        var keyBytes = prefixedBytes[Const.SECP256K1_DID_PREFIX.Length..];
        return VerifySig(keyBytes, data, sig, opts);
    }

    public static bool VerifySig(byte[] publicKey, byte[] data, byte[] sig, VerifyOptions? opts)
    {
        var allowMalleable = opts?.AllowMalleableSig ?? false;
        var msgHash = SHA256.HashData(data);
        if (!allowMalleable && !IsCompactFormat(sig))
        {
            return false;
        }
        
        return Secp256K1.Verify(sig, msgHash, publicKey);
    }

    public static bool IsCompactFormat(byte[] sig)
    {
        var outBuf = new byte[64];
        if (!Secp256K1.SignatureParseCompact(outBuf, sig))
        {
            return false;
        }
            
        var compactBuf = new byte[64];
        if (!Secp256K1.SignatureSerializeCompact(compactBuf, outBuf))
        {
            return false;
        }
            
        return sig.SequenceEqual(compactBuf);
    }
}