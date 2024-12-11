using System.Security.Cryptography;
using CID;
using Multiformats.Codec;
using Multiformats.Hash;
using PeterO.Cbor;

namespace Common;

public class CborBlock(CBORObject value, byte[] bytes, Cid cid)
{
    public static CborBlock Encode(CBORObject obj)
    {
        var buffer = obj.EncodeToBytes();
        
        var hash = Multihash.Encode(SHA256.HashData(buffer), HashType.SHA2_256);
        var cid = Cid.NewV1((ulong)MulticodecCode.CBOR, hash);
        
        return new CborBlock(obj, buffer, cid);
    }
    
    public static CborBlock Encode<T>(ICborEncodable<T> obj)
    {
        return Encode(obj.ToCborObject());
    }
    
    public static CBORObject Decode(byte[] bytes)
    {
        return CBORObject.DecodeFromBytes(bytes);
    }
    public Cid Cid { get; set; } = cid;

    public byte[] Bytes { get; set; } = bytes;

    public CBORObject Value { get; set; } = value;
}

public interface ICborEncodable<out T>
{
    CBORObject ToCborObject();
    
    static abstract T FromCborObject(CBORObject obj);
}