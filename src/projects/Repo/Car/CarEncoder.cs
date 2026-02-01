using Ipfs;
using PeterO.Cbor;
using Cid = CID.Cid;

namespace Repo.Car;

public static class CarEncoder
{
    public static byte[] EncodeRoots(Cid? root) =>
        CreateHeader(root);

    // Note: spec supports multiple roots
    private static byte[] CreateHeader(Cid? root)
    {
        var headerObj = CBORObject.NewMap();

        // Note: empty roots is not well specified in the CAR spec
        // https://ipld.io/specs/transport/car/carv1/#number-of-roots
        // CAR files encoded with empty roots don't seem to be readable from https://explore.ipld.io/
        // you can add the empty CID workaround they mention, but I will stick to what the js implementation does which is empty array

        headerObj.Add("roots", root is null
            ? CBORObject.NewArray()
            : CBORObject.NewArray().Add(root.Value.ToCBORObject()));
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