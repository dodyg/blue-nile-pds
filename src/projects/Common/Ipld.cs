using CID;

namespace Common;

public class Ipld
{
    public static void VerifyCidForBytes(Cid cid, byte[] bytes)
    {
        var digest = Util.Sha2_256Digest(bytes);
        var expected = Cid.NewV1(cid.Codec, digest);
        if (cid != expected)
        {
            throw new Exception($"Not a valid CID for bytes. Expected: {expected.ToString()} Got: {cid.ToString()}.");
        }
    }
}