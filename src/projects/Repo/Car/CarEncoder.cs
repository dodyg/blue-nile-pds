using Ipfs;
using PeterO.Cbor;
using Cid = CID.Cid;

namespace Repo.Car;

public static class CarEncoder
{
    public static byte[] EncodeRoots(Cid root) =>
        CreateHeader(root);

    // Note: spec supports multiple roots
    private static byte[] CreateHeader(Cid root)
    {
        var headerObj = CBORObject.NewMap();
        headerObj.Add("roots", new[] {root.ToCBORObject()});
        headerObj.Add("version", 1);
        var headerBytes = headerObj.EncodeToBytes()!;
        var varIntBytes = Varint.Encode(headerBytes.Length);
        return [..varIntBytes, ..headerBytes];
    }

    public static byte[] EncodeBlock(CarBlock block)
    {
        var cidBytes = block.Cid.ToBytes();
        var varIntBytes = Varint.Encode(cidBytes.Length + block.Bytes.Length);
        byte[] buffer = [..varIntBytes, ..cidBytes, ..block.Bytes];
        return buffer;
    }
}