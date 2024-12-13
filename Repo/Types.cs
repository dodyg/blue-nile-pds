using CID;
using Common;
using PeterO.Cbor;
using Repo.MST;

namespace Repo;

public record CommitData(Cid Cid, string Rev, string? Since, Cid? Prev, BlockMap NewBlocks, CidSet RemovedCids)
{
    public int Version => 3;
}

public record Commit(string Did, Cid Data, string Rev, Cid? Prev, byte[] Sig) : ICborEncodable<Commit>
{
    public int Version => 3;
    
    public CBORObject ToCborObject()
    {
        var obj = CBORObject.NewMap();
        obj.Add("did", Did);
        obj.Add("data", Data.ToCBORObject());
        obj.Add("rev", Rev);
        obj.Add("prev", Prev?.ToCBORObject());
        obj.Add("sig", Sig);
        obj.Add("version", Version);
        return obj;
    }
    
    public static Commit FromCborObject(CBORObject obj)
    {
        var did = obj["did"].AsString();
        var data = Cid.FromCBOR(obj["data"]);
        var rev = obj["rev"].AsString();
        Cid? prev = obj.ContainsKey("prev") && !obj["prev"].IsNull ? Cid.FromCBOR(obj["prev"]) : null;
        var sig = obj["sig"].GetByteString();
        return new Commit(did, data, rev, prev, sig);
    }
}

public record UnsignedCommit(string Did, Cid Data, string Rev, Cid? Prev) : ICborEncodable<UnsignedCommit>
{
    public int Version => 3;
    
    public CBORObject ToCborObject()
    {
        var obj = CBORObject.NewMap();
        obj.Add("did", Did);
        obj.Add("data", Data.ToCBORObject());
        obj.Add("rev", Rev);
        obj.Add("prev", Prev?.ToCBORObject());
        obj.Add("version", Version);
        return obj;
    }
    
    public static UnsignedCommit FromCborObject(CBORObject obj)
    {
        var did = obj["did"].AsString();
        var data = Cid.FromCBOR(obj["data"]);
        var rev = obj["rev"].AsString();
        Cid? prev = obj.ContainsKey("prev") && !obj["prev"].IsNull ? Cid.FromCBOR(obj["prev"]) : null;
        return new UnsignedCommit(did, data, rev, prev);
    }
}
public record Entry(Cid Cid, byte[] Block);