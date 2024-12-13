using Multiformats.Base;
using Multiformats.Codec;
using Multiformats.Hash;
using PeterO.Cbor;
using Exception = System.Exception;

namespace CID;

public readonly record struct Cid
{
    public const ulong DAG_PB = 0x70;
    public const ulong SHA2_256 = (uint)HashType.SHA2_256; // 0x12
    public const string IPFS_DELIM = "/ipfs/";
    
    public Version Version { get; private init; }
    public ulong Codec { get; private init; }
    public Multihash Hash { get; private init; }
    

    // Check if the version of `data` string is CIDv0.
    // v0 is a Base58Btc encoded sha hash, so it has
    // fixed length and always begins with "Qm"
    public static bool IsV0Str(string data) => data.Length == 46 && data.StartsWith("Qm");

    // Check if the version of `data` bytes is CIDv0.
    public static bool IsV0Binary(byte[] data) => data.Length == 34 && data[0] == Cid.SHA2_256 && data[1] == 0x20;

    private void AssertCidV0()
    {
        if (Version != Version.V0)
        {
            throw new CIDException(Error.InvalidCidVersion);
        }

        if (Codec != DAG_PB)
        {
            throw new CIDException(Error.InvalidCidV0Codec);
        }

        if (Hash.Code != HashType.SHA2_256 || Hash.Length != 32)
        {
            throw new CIDException(Error.InvalidCidV0Multihash);
        }
    }
    
    public override string ToString()
    {
        return ToStringOfBase(MultibaseEncoding.Base58Btc);
    }
    
    public static Cid NewV0(Multihash hash) => StrictNewV0(Version.V0, DAG_PB, hash);

    public static Cid NewV1(ulong codec, Multihash hash) => StrictNewV1(Version.V1, codec, hash);

    public static Cid New(Version version, ulong codec, Multihash hash) => version switch
    {
        Version.V0 => StrictNewV0(version, codec, hash),
        Version.V1 => StrictNewV1(version, codec, hash),
        _ => throw new CIDException(Error.InvalidCidVersion)
    };
    
    private static Cid StrictNewV0(Version version, ulong codec, Multihash hash)
    {
        if (version != Version.V0)
        {
            throw new CIDException(Error.InvalidCidVersion);
        }

        if (codec != DAG_PB)
        {
            throw new CIDException(Error.InvalidCidV0Codec);
        }

        if (hash.Code != HashType.SHA2_256 || hash.Length != 32)
        {
            throw new CIDException(Error.InvalidCidV0Multihash);
        }

        return new Cid {Version = version, Codec = codec, Hash = hash};
    }

    private static Cid StrictNewV1(Version version, ulong codec, Multihash hash)
    {
        if (version != Version.V1)
        {
            throw new CIDException(Error.InvalidCidVersion);
        }

        return new Cid {Version = version, Codec = codec, Hash = hash};
    }

    private Cid V0ToV1()
    {
        AssertCidV0();
        return StrictNewV1(Version.V1, DAG_PB, Hash);
    }
    
    public Cid ToV1() => Version switch
    {
        Version.V0 => V0ToV1(),
        Version.V1 => this,
        _ => throw new CIDException(Error.InvalidCidVersion)
    };

    public static Cid ReadBytes(byte[] bytes)
    {
        if (bytes.Length < 2)
        {
            throw new CIDException(Error.InputTooShort);
        }

        var version = bytes[0];
        var codec = (ulong)bytes[1];
        var digest = bytes[2..];

        if (version == SHA2_256 && codec == 0x20)
        {
            if (digest.Length != 32)
            {
                throw new CIDException(Error.InvalidCidV0Multihash);
            }

            var mh = Multihash.Encode(digest, (HashType)SHA2_256);
            return StrictNewV0(Version.V0, DAG_PB, mh);
        }

        var versionType = version.ParseVersion();
        return versionType switch
        {
            Version.V0 => throw new CIDException(Error.InvalidExplicitCidV0),
            Version.V1 => StrictNewV1(versionType, codec, Multihash.Decode(digest)),
            _ => throw new CIDException(Error.InvalidCidVersion)
        };
    }

    public byte[] ToBytes()
    {
        return Version switch
        {
            Version.V0 => V0ToBytes(),
            Version.V1 => V1ToBytes(),
            _ => throw new CIDException(Error.InvalidCidVersion)
        };
    }

    public string ToStringOfBase(MultibaseEncoding encoding)
    {
        return Version switch
        {
            Version.V0 when encoding == MultibaseEncoding.Base58Btc => ToStringV0(),
            Version.V0 => throw new CIDException(Error.InvalidCidV0Base),
            Version.V1 => Multibase.Encode(encoding, ToBytes()),
            _ => throw new CIDException(Error.InvalidCidVersion)
        };
    }
    
    private string ToStringV0()
    {
        AssertCidV0();
        return Multibase.Base58.Encode(Hash.ToBytes());
    }

    private byte[] V0ToBytes()
    {
        return Hash.ToBytes();
    }

    private byte[] V1ToBytes()
    {
        var hashBytes = Hash.ToBytes();
        return [(byte)Version, (byte)Codec, ..hashBytes];
    }
    
    
    public static Cid FromString(string cidStr)
    {
        string hash;
        var ipfsDelimIndex = cidStr.IndexOf(IPFS_DELIM, StringComparison.Ordinal);
        if (ipfsDelimIndex != -1)
        {
            hash = cidStr[(ipfsDelimIndex + IPFS_DELIM.Length)..];
        }
        else
        {
            hash = cidStr;
        }

        if (hash.Length < 2)
        {
            throw new CIDException(Error.InputTooShort);
        }

        var decoded = IsV0Str(hash) ? Multibase.Base58.Decode(hash) : Multibase.Decode(hash, out MultibaseEncoding enc);

        return ReadBytes(decoded);
    }

    public static Cid Create(string data)
    {
        var digest = Util.Sha2_256Digest(data);
        return NewV1((ulong)MulticodecCode.Raw, digest);
    }
    
    public static CBORObject ToCBORObject(Cid obj)
    {
        byte[] bytes = [0x00, ..obj.ToBytes()];
        return CBORObject.FromObjectAndTag(bytes, 42);
    }
    
    public CBORObject ToCBORObject()
    {
        return ToCBORObject(this);
    }
    
    public static Cid FromCBOR(CBORObject obj)
    {
        if (obj.Type != CBORType.ByteString)
        {
            throw new Exception("Invalid CBOR type for CID");
        }

        var bytes = obj.GetByteString();
        if (bytes[0] != 0x00)
        {
            throw new Exception("Invalid CBOR tag for CID");
        }
        return ReadBytes(bytes[1..]);
    }
}