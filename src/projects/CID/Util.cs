using System.Security.Cryptography;
using System.Text;
using Multiformats.Codec;
using Multiformats.Hash;

namespace CID;

public static class Util
{
    public static Version ParseVersion(this byte version)
    {
        return version switch
        {
            0 => Version.V0,
            1 => Version.V1,
            _ => throw new CIDException(Error.UnknownCodec)
        };
    }

    public static Multihash Sha2_256Digest(string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        var hash = SHA256.HashData(bytes);
        return Multihash.Encode(hash, HashType.SHA2_256);
    }
    public static Multihash Sha2_256Digest(Stream stream)
    {
        var hash = SHA256.HashData(stream);
        return Multihash.Encode(hash, HashType.SHA2_256);
    }

    public static Multihash Sha2_256Digest(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Multihash.Encode(hash, HashType.SHA2_256);
    }


    /// <summary>
    /// Generates a CID with the 'blessed' format for blobs
    /// <para> https://atproto.com/specs/blob#blob-metadata </para>
    /// </summary>
    public static Cid CidForBlobs(Stream blobStream)
    {
        var multiHash = Sha2_256Digest(blobStream);
        return Cid.NewV1((ulong) MulticodecCode.Raw, multiHash);
    }
    /// <summary>
    /// Generates a CID with the 'blessed' format for blobs
    /// <para> https://atproto.com/specs/blob#blob-metadata </para>
    /// </summary>
    public static Cid CidForBlobs(byte[] data)
    {
        var multiHash = Sha2_256Digest(data);
        return Cid.NewV1((ulong) MulticodecCode.Raw, multiHash);
    }
}