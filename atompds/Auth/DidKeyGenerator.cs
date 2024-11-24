using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using SimpleBase;

namespace atompds.Enc;

public class DidKeyGenerator
{
    // Constants for multicodec
    private const byte SECP256K1_PREFIX = 0xE7;
    private const string BASE58BTC_PREFIX = "z";
    private const string DID_KEY_PREFIX = "did:key:";
    
    public static AsymmetricCipherKeyPair GenerateKeyPair()
    {
        // Get the SECP256K1 curve parameters
        X9ECParameters curve = ECNamedCurveTable.GetByName("secp256k1");
        var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);

        // Generate the key pair
        var generator = new ECKeyPairGenerator();
        var secureRandom = new SecureRandom();
        var keyGenParam = new ECKeyGenerationParameters(domain, secureRandom);
        generator.Init(keyGenParam);
        return generator.GenerateKeyPair();
    }
    
    public static (string privateKey, string publicKey) ToHexStrings(AsymmetricCipherKeyPair keyPair)
    {
        var privateKey = (ECPrivateKeyParameters)keyPair.Private;
        var publicKey = (ECPublicKeyParameters)keyPair.Public;
        return (ToHex(privateKey.D.ToByteArrayUnsigned()), ToHex(publicKey.Q.GetEncoded()));
    }
    
    public static AsymmetricCipherKeyPair GenerateKeyPairFromStrings(string privateKey, string publicKey)
    {
        var curve = ECNamedCurveTable.GetByName("secp256k1");
        var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);
        var privKey = new ECPrivateKeyParameters(new BigInteger(1, FromHex(privateKey)), domain);
        var pubKey = new ECPublicKeyParameters(curve.Curve.DecodePoint(FromHex(publicKey)), domain);
        return new AsymmetricCipherKeyPair(pubKey, privKey);
    }

    public static string EncodeDidPubKey(ECPublicKeyParameters publicKey)
    {
        // Get compressed public key bytes
        byte[] compressedPublicBytes = publicKey.Q.GetEncoded(true);

        // Prepare multicodec wrapped bytes
        byte[] wrappedBytes = new byte[compressedPublicBytes.Length + 2];
        wrappedBytes[0] = SECP256K1_PREFIX;
        wrappedBytes[1] = (byte)compressedPublicBytes.Length;
        Array.Copy(compressedPublicBytes, 0, wrappedBytes, 2, compressedPublicBytes.Length);

        // Encode with Base58BTC
        string base58Encoded = Base58.Bitcoin.Encode(wrappedBytes);
        
        // Return complete DID key
        return $"{DID_KEY_PREFIX}{BASE58BTC_PREFIX}{base58Encoded}";
    }
    
    private static string ToHex(byte[] data) => String.Concat(data.Select(x => x.ToString("x2")));
    private static byte[] FromHex(string hex) => Enumerable.Range(0, hex.Length)
        .Where(x => x % 2 == 0)
        .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
        .ToArray();
}