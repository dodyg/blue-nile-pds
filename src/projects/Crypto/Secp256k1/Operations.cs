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

        if (IsCompactFormat(sig))
        {
            var outBuf = new byte[Secp256k1Net.Secp256k1.UNSERIALIZED_SIGNATURE_LENGTH];
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
        var outBuf = new byte[Secp256k1Net.Secp256k1.UNSERIALIZED_SIGNATURE_LENGTH];
        if (!Secp256k1Wrapper.SignatureParseCompact(outBuf, sig))
        {
            return false;
        }

        var compactBuf = new byte[Secp256k1Net.Secp256k1.SERIALIZED_SIGNATURE_SIZE];
        if (!Secp256k1Wrapper.SignatureSerializeCompact(compactBuf, outBuf))
        {
            return false;
        }

        return sig.SequenceEqual(compactBuf);
    }
}
