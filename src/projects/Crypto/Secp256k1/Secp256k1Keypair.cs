using System.Security.Cryptography;

namespace Crypto.Secp256k1;

public class Secp256k1Keypair : IExportableKeyPair
{
    private readonly bool _exportable;
    private readonly byte[] _privateKey;
    private readonly byte[] _publicKey;

    public Secp256k1Keypair(string privateKey, bool exportable) : this(Convert.FromHexString(privateKey), exportable)
    {
    }

    public Secp256k1Keypair(byte[] privateKey, bool exportable)
    {
        if (privateKey.Length != Secp256k1Net.Secp256k1.SECRET_KEY_LENGTH)
        {
            throw new ArgumentException("Invalid private key length");
        }

        if (!Secp256k1Wrapper.SecretKeyVerify(privateKey))
        {
            throw new ArgumentException("Invalid private key");
        }

        _privateKey = privateKey;
        _exportable = exportable;
        _publicKey = Secp256k1Wrapper.PublicKeyCreate(privateKey);
    }

    public byte[] Export()
    {
        if (!_exportable)
        {
            throw new Exception("Key is not exportable");
        }

        return _privateKey;
    }

    public string JwtAlg => Const.SECP256K1_JWT_ALG;
    public byte[] Sign(byte[] data)
    {
        var msgHash = SHA256.HashData(data);
        return Secp256k1Wrapper.Sign(msgHash, _privateKey);
    }

    public string Did()
    {
        return Crypto.Did.FormatDidKey(JwtAlg, _publicKey);
    }

    public static Secp256k1Keypair Create(bool exportable)
    {
        var privateKey = new byte[Secp256k1Net.Secp256k1.SECRET_KEY_LENGTH];
        var rnd = RandomNumberGenerator.Create();
        do
        {
            rnd.GetBytes(privateKey);
        } while (!Secp256k1Wrapper.SecretKeyVerify(privateKey));

        return new Secp256k1Keypair(privateKey, exportable);
    }

    public static Secp256k1Keypair Import(string privateKey, bool exportable = false)
    {
        return new Secp256k1Keypair(privateKey, exportable);
    }

    public static Secp256k1Keypair Import(byte[] privateKey, bool exportable = false)
    {
        return new Secp256k1Keypair(privateKey, exportable);
    }
}
