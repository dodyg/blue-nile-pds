using CID;
using Common;
using Crypto;
using PeterO.Cbor;
using Repo.MST;

namespace Repo;

public record CommitData(Cid Cid, string Rev, string? Since, Cid? Prev, BlockMap NewBlocks, CidSet RemovedCids)
{
    public int Version => 3;
}

public record Commit(string Did, Cid Data, string Rev, Cid? Prev, byte[] Sig) : ICborEncodable
{
    public int Version => 3;
    
    public CBORObject ToCborObject()
    {
        var obj = CBORObject.NewMap();
        obj.Add("Did", Did);
        obj.Add("Data", Data.ToString());
        obj.Add("Rev", Rev);
        if (Prev != null)
        {
            obj.Add("Prev", Prev.ToString());
        }
        obj.Add("Sig", CBORObject.FromObject(Sig));
        obj.Add("Version", Version);
        return obj;
    }
    
    public static Commit FromCborObject(CBORObject obj)
    {
        var did = obj["Did"].AsString();
        var data = Cid.FromString(obj["Data"].AsString());
        var rev = obj["Rev"].AsString();
        Cid? prev = obj.ContainsKey("Prev") ? Cid.FromString(obj["Prev"].AsString()) : null;
        var sig = obj["Sig"].GetByteString();
        return new Commit(did, data, rev, prev, sig);
    }
}

public record UnsignedCommit(string Did, Cid Data, string Rev, Cid? Prev) : ICborEncodable
{
    public int Version => 3;
    
    public CBORObject ToCborObject()
    {
        var obj = CBORObject.NewMap();
        obj.Add("Did", Did);
        obj.Add("Data", Data.ToString());
        obj.Add("Rev", Rev);
        if (Prev != null)
        {
            obj.Add("Prev", Prev.ToString());
        }
        obj.Add("Version", Version);
        return obj;
    }
}
public record Entry(Cid Cid, byte[] Block);

public static class Util
{
    public static Commit SignCommit(UnsignedCommit unsigned, IKeyPair keypair)
    {
        var encoded = CborBlock.Encode(unsigned);
        var sig = keypair.Sign(encoded.Bytes);
        return new Commit(unsigned.Did, unsigned.Data, unsigned.Rev, unsigned.Prev, sig);
    }
}