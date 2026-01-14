using CID;
using Common;
using Crypto;
using FishyFlip.Models;
using PeterO.Cbor;
using Repo.MST;

namespace Repo;

public class Repo
{
    private readonly IRepoStorage _storage;

    public Repo(Params p)
    {
        _storage = p.Storage;
        Data = p.Data;
        Commit = p.Commit;
        Cid = p.Cid;
    }
    public MST.MST Data { get; set; }
    public Commit Commit { get; set; }
    public Cid Cid { get; set; }

    public static async Task<CommitData> FormatInitCommit(IRepoStorage storage, string did, IKeyPair keypair, RecordCreateOp[]? initialWrites = null)
    {
        initialWrites ??= [];
        var newBlocks = new BlockMap();
        var data = MST.MST.Create(storage, []);
        foreach (var record in initialWrites)
        {
            var cid = newBlocks.Add(record.Record);
            var dataKey = MST.Util.FormatDataKey(record.Collection, record.RKey);
            data = await data.Add(dataKey, cid);
        }

        var dataCid = await data.GetPointer();
        var diff = await DataDiff.Of(data, null);
        newBlocks.AddMap(diff.NewMstBlocks);

        var rev = TID.NextStr();
        var commit = Util.SignCommit(new UnsignedCommit(did, dataCid, rev, null), keypair);
        var commitCid = newBlocks.Add(commit.ToCborObject());
        return new CommitData(commitCid, rev, null, null, newBlocks, diff.RemovedCids);
    }

    public static async Task<Repo> CreateFromCommit(IRepoStorage storage, CommitData commit)
    {
        await storage.ApplyCommit(commit);
        return await Load(storage, commit.Cid);
    }

    public static async Task<Repo> Create(IRepoStorage storage, string did, IKeyPair keypair, RecordCreateOp[]? initialWrites = null)
    {
        var commit = await FormatInitCommit(storage, did, keypair, initialWrites);
        return await CreateFromCommit(storage, commit);
    }

    public static async Task<Repo> Load(IRepoStorage storage, Cid? cid)
    {
        var commitCid = cid ?? await storage.GetRoot();
        if (commitCid == null)
        {
            throw new Exception("No root commit found");
        }

        var (obj, bytes) = await storage.ReadObjAndBytes(commitCid.Value);
        var commit = Commit.FromCborObject(obj);
        var data = MST.MST.Load(storage, commit.Data);
        return new Repo(new Params(storage, data, commit, commitCid.Value));
    }

    public async Task<CommitData> FormatCommit(IRecordWriteOp[] toWrite, IKeyPair keypair)
    {
        var leaves = new BlockMap();
        var data = Data;
        foreach (var write in toWrite)
        {
            if (write is RecordCreateOp create)
            {
                var cid = leaves.Add(create.Record);
                var dataKey = $"{create.Collection}/{create.RKey}";
                data = await data.Add(dataKey, cid);
                //data = await data.Update(dataKey, cid);
            }
            else if (write is RecordUpdateOp update)
            {
                var cid = leaves.Add(update.Record);
                var dataKey = $"{update.Collection}/{update.RKey}";
                data = await data.Update(dataKey, cid);
            }
            else if (write is RecordDeleteOp delete)
            {
                var dataKey = $"{delete.Collection}/{delete.RKey}";
                data = await data.Delete(dataKey);
            }
        }

        var dataCid = await data.GetPointer();
        var diff = await DataDiff.Of(data, Data);
        var newBlocks = diff.NewMstBlocks;
        var removedCids = diff.RemovedCids;

        var addedLeaves = leaves.GetMany(diff.NewLeafCids.ToArray());
        if (addedLeaves.missing.Length > 0)
        {
            throw new Exception($"Missing leaves: {string.Join(", ", addedLeaves.missing)}");
        }

        newBlocks.AddMap(addedLeaves.blocks);

        var rev = TID.NextStr(Commit.Rev);
        var commit = Util.SignCommit(new UnsignedCommit(Commit.Did, dataCid, rev, null), keypair);
        var commitCid = newBlocks.Add(commit.ToCborObject());

        if (commitCid == Cid)
        {
            newBlocks.Delete(Cid);
        }
        else
        {
            removedCids.Add(Cid);
        }

        return new CommitData(commitCid, rev, commit.Rev, Cid, newBlocks, removedCids);
    }

    public async Task<Repo> ApplyCommit(CommitData commit)
    {
        await _storage.ApplyCommit(commit);
        return await Load(_storage, commit.Cid);
    }

    public async Task<Repo> ApplyWrites(IRecordWriteOp[] toWrite, IKeyPair keypair)
    {
        var commit = await FormatCommit(toWrite, keypair);
        return await ApplyCommit(commit);
    }
    public record Params(IRepoStorage Storage, MST.MST Data, Commit Commit, Cid Cid);
}

public enum WriteOpAction
{
    Create,
    Update,
    Delete
}

public enum ValidationStatus
{
    Valid,
    Unknown
}

public interface IPreparedWrite
{
    public WriteOpAction Action { get; }
    public ATUri Uri { get; }
}

public interface IPreparedDataWrite : IPreparedWrite
{
    public Cid Cid { get; }
    public PreparedBlobRef[] Blobs { get; }
}

public record BlobConstraint(string[]? Accept, long? MaxSize);
public record PreparedBlobRef(Cid Cid, string MimeType, long Size, BlobConstraint Constraints);

public record PreparedCreate(ATUri Uri, Cid Cid, Cid? SwapCid, CBORObject Record, PreparedBlobRef[] Blobs, ValidationStatus ValidationStatus) : IPreparedDataWrite
{
    public WriteOpAction Action => WriteOpAction.Create;

    public RecordCreateOp CreateWriteToOp()
    {
        return new RecordCreateOp(Uri.Collection, Uri.Rkey, Record);
    }
}

public record PreparedUpdate(ATUri Uri, Cid Cid, Cid? SwapCid, CBORObject Record, PreparedBlobRef[] Blobs, ValidationStatus ValidationStatus) : IPreparedDataWrite
{
    public WriteOpAction Action => WriteOpAction.Update;

    public RecordUpdateOp UpdateWriteToOp()
    {
        return new RecordUpdateOp(Uri.Collection, Uri.Rkey, Record);
    }
}

public record PreparedDelete(ATUri Uri, Cid? SwapCid) : IPreparedWrite
{
    public WriteOpAction Action => WriteOpAction.Delete;

    public RecordDeleteOp DeleteWriteToOp()
    {
        return new RecordDeleteOp(Uri.Collection, Uri.Rkey);
    }
}

public interface IRecordWriteOp
{
    public WriteOpAction Action { get; }
}

public record RecordCreateOp(string Collection, string RKey, CBORObject Record) : IRecordWriteOp
{
    public WriteOpAction Action => WriteOpAction.Create;
}

public record RecordUpdateOp(string Collection, string RKey, CBORObject Record) : IRecordWriteOp
{
    public WriteOpAction Action => WriteOpAction.Update;
}

public record RecordDeleteOp(string Collection, string RKey) : IRecordWriteOp
{
    public WriteOpAction Action => WriteOpAction.Delete;
}