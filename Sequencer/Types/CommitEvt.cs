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
        cbor.Add("commit", Commit.ToString());
        if (Prev != null)
        {
            cbor.Add("prev", Prev.ToString());
        }
        else
        {
            cbor.Add("prev", CBORObject.Null);
        }
        cbor.Add("rev", Rev);
        if (Since != null)
        {
            cbor.Add("since", Since);
        }
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
            blobsArr.Add(blob.ToString());
        }
        cbor.Add("blobs", blobsArr);
        return cbor;
    }
    
    public static CommitEvt FromCborObject(CBORObject cbor)
    {
        var rebase = cbor["rebase"].AsBoolean();
        var tooBig = cbor["tooBig"].AsBoolean();
        var repo = cbor["repo"].AsString();
        var commit = cbor["commit"].AsString();
        Cid? prev = null;
        if (cbor.ContainsKey("prev"))
        {
            var prevCbor = cbor["prev"];
            if (prevCbor == null || prevCbor.IsNull)
            {
                prev = null;
            }
            else
            {
                prev = Cid.FromString(prevCbor.AsString());
            }
        }
        var rev = cbor["rev"].AsString();
        string? since = null;
        if (cbor.ContainsKey("since"))
        {
            since = cbor["since"].AsString();
        }
        var blocks = cbor["blocks"].GetByteString();
        var ops = cbor["ops"].Values.Select(CommitEvtOp.FromCborObject).ToArray();
        var blobs = cbor["blobs"].Values.Select(x => CID.Cid.FromString(x.AsString())).ToArray();
        return new CommitEvt
        {
            Rebase = rebase,
            TooBig = tooBig,
            Repo = repo,
            Commit = Cid.FromString(commit),
            Prev = prev,
            Rev = rev,
            Since = since,
            Blocks = blocks,
            Ops = ops,
            Blobs = blobs
        };
    }
}