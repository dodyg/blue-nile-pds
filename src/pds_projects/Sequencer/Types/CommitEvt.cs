using CID;
using Common;
using PeterO.Cbor;

namespace Sequencer.Types;

public record CommitEvt : ICborEncodable<CommitEvt>
{
    public required bool Rebase { get; init; }
    public required bool TooBig { get; init; }
    public required string Repo { get; init; }
    public required Cid Commit { get; init; }
    public required Cid? Prev { get; init; }
    public required string Rev { get; init; }
    public required string? Since { get; init; }
    public required byte[] Blocks { get; init; }
    public required CommitEvtOp[] Ops { get; init; }
    public required Cid[] Blobs { get; init; }


    public CBORObject ToCborObject()
    {
        var cbor = CBORObject.NewMap();
        cbor.Add("rebase", Rebase);
        cbor.Add("tooBig", TooBig);
        cbor.Add("repo", Repo);
        cbor.Add("commit", Commit.ToCBORObject());
        cbor.Add("prev", Prev?.ToCBORObject());
        cbor.Add("rev", Rev);
        cbor.Add("since", Since);
        cbor.Add("blocks", Blocks);
        var opsArr = CBORObject.NewArray();
        foreach (var op in Ops)
        {
            opsArr.Add(op.ToCborObject());
        }
        cbor.Add("ops", opsArr);
        var blobsArr = CBORObject.NewArray();
        foreach (var blob in Blobs)
        {
            blobsArr.Add(blob.ToCBORObject());
        }
        cbor.Add("blobs", blobsArr);
        return cbor;
    }

    public static CommitEvt FromCborObject(CBORObject cbor)
    {
        var rebase = cbor["rebase"].AsBoolean();
        var tooBig = cbor["tooBig"].AsBoolean();
        var repo = cbor["repo"].AsString();
        var rev = cbor["rev"].AsString();
        var since = cbor.ContainsKey("since") && !cbor["since"].IsNull ? cbor["since"].AsString() : null;
        var blocks = cbor["blocks"].GetByteString();
        var ops = cbor["ops"].Values.Select(CommitEvtOp.FromCborObject).ToArray();
        var blobs = cbor["blobs"].Values.Select(Cid.FromCBOR).ToArray();
        return new CommitEvt
        {
            Rebase = rebase,
            TooBig = tooBig,
            Repo = repo,
            Commit = Cid.FromCBOR(cbor["commit"]),
            Prev = cbor["prev"].IsNull ? null : Cid.FromCBOR(cbor["prev"]),
            Rev = rev, Since = since,
            Blocks = blocks,
            Ops = ops,
            Blobs = blobs
        };
    }
}