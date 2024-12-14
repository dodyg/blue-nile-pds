using CID;
using Common;
using PeterO.Cbor;

namespace Sequencer.Types;

public record CommitEvtOp : ICborEncodable<CommitEvtOp>
{
    public required CommitEvtAction Action { get; init; }
    public required string Path { get; init; }
    public required Cid? Cid { get; init; }

    public CBORObject ToCborObject()
    {
        var cbor = CBORObject.NewMap();
        cbor.Add("action", Action.ToString().ToLower());
        cbor.Add("path", Path);
        if (Cid != null)
        {
            cbor.Add("cid", Cid.Value.ToCBORObject());
        }
        return cbor;
    }

    public static CommitEvtOp FromCborObject(CBORObject cbor)
    {
        var actionTxt = cbor["action"].AsString().ToUpper();
        var action = Enum.Parse<CommitEvtAction>(actionTxt, true);
        var path = cbor["path"].AsString();
        Cid? cid = cbor.ContainsKey("cid") && !cbor["cid"].IsNull ? CID.Cid.FromCBOR(cbor["cid"]) : null;
        return new CommitEvtOp
        {
            Action = action,
            Path = path,
            Cid = cid
        };
    }
}