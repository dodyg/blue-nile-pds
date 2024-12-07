using System.Security.Cryptography;

namespace Crypto.Secp256k1;

public class Operations
{
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
        
        // fix pkey length
        if (publicKey.Length == 33)
        {
            var buf = new byte[64];
            if (!Secp256k1Wrapper.PublicKeyParse(buf, publicKey))
            {
                return false;
            }
            
            publicKey = buf;
        }
        
        if (IsCompactFormat(sig))
        {
            var outBuf = new byte[64];
            if (!Secp256k1Wrapper.SignatureParseCompact(outBuf, sig))
            {
                return false;
            }
            
            sig = outBuf;
        }
        
        return Secp256k1Wrapper.Verify(sig, msgHash, publicKey);
    }

    public static bool IsCompactFormat(byte[] sig)
    {
        var outBuf = new byte[64];
        if (!Secp256k1Wrapper.SignatureParseCompact(outBuf, sig))
        {
            return false;
        }
            
        var compactBuf = new byte[64];
        if (!Secp256k1Wrapper.SignatureSerializeCompact(compactBuf, outBuf))
        {
            return false;
        }
            
        return sig.SequenceEqual(compactBuf);
    }
}