using System.Security.Cryptography;

namespace Crypto.Secp256k1;

public class Secp256k1Keypair : IExportableKeyPair
{
    private readonly byte[] _publicKey;
    private readonly bool _exportable;
    private readonly byte[] _privateKey;
        
    public Secp256k1Keypair(string privateKey, bool exportable) : this(Convert.FromHexString(privateKey), exportable)
    {
    }
    
    public Secp256k1Keypair(byte[] privateKey, bool exportable)
    {
        if (privateKey.Length != Secp256k1Net.Secp256k1.PRIVKEY_LENGTH)
        {
            throw new ArgumentException("Invalid private key length");
        }

        if (!Secp256k1Wrapper.SecretKeyVerify(privateKey))
        {
            throw new ArgumentException("Invalid private key");
        }

        var publicKey = new byte[Secp256k1Net.Secp256k1.PUBKEY_LENGTH];
        if (!Secp256k1Wrapper.PublicKeyCreate(publicKey, privateKey))
        {
            throw new Exception("Failed to create public key");
        }

        _privateKey = privateKey;
        _exportable = exportable;
        _publicKey = publicKey;
    }

    public static Secp256k1Keypair Create(bool exportable)
    {
        var privateKey = new byte[Secp256k1Net.Secp256k1.PRIVKEY_LENGTH];
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
        var signature = new byte[Secp256k1Net.Secp256k1.SIGNATURE_LENGTH];
        if (!Secp256k1Wrapper.Sign(signature, msgHash, _privateKey))
        {
            throw new Exception("Failed to sign data");
        }

        var compactSig = new byte[Secp256k1Net.Secp256k1.SIGNATURE_LENGTH];
        if (!Secp256k1Wrapper.SignatureSerializeCompact(compactSig, signature))
        {
            throw new Exception("Failed to sign data");
        }

        return compactSig;
    }
    
    public string Did()
    {
        return Crypto.Did.FormatDidKey(JwtAlg, _publicKey);
    }
}