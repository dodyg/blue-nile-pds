namespace Crypto;

public static class Const
{
    public static readonly byte[] P256_DID_PREFIX = [0x80, 0x24];
    public static readonly byte[] SECP256K1_DID_PREFIX = [0xE7, 0x01];
    public const string BASE58_MULTIBASE_PREFIX = "z";
    public const string DID_KEY_PREFIX = "did:key:";
    public const string P256_JWT_ALG = "ES256";
    public const string SECP256K1_JWT_ALG = "ES256K";
}