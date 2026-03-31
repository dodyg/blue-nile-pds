namespace Crypto.Secp256k1;

public class Encoding
{
    public static byte[] CompressPubKey(byte[] pubKeyBytes)
    {
        return Secp256k1Wrapper.CompressPublicKey(pubKeyBytes);
    }

    public static byte[] DecompressPubKey(byte[] pubKeyBytes)
    {
        return Secp256k1Wrapper.DecompressPublicKey(pubKeyBytes);
    }
}
