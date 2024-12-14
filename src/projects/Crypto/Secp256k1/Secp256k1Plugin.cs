namespace Crypto.Secp256k1;

public class Secp256k1Plugin : IDidKeyPlugin
{

    public byte[] Prefix => Const.SECP256K1_DID_PREFIX;
    public string JwtAlg => Const.SECP256K1_JWT_ALG;

    public bool VerifySignature(string did, byte[] msg, byte[] data, VerifyOptions? options)
    {
        return Operations.VerifyDidSig(did, msg, data, options);
    }
    public byte[] CompressPubKey(byte[] uncompressed)
    {
        return Encoding.CompressPubKey(uncompressed);
    }
    public byte[] DecompressPubKey(byte[] compressed)
    {
        return Encoding.DecompressPubKey(compressed);
    }
}