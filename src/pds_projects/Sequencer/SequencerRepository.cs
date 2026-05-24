using System.Threading.Channels;
using AccountManager.Db;
using CID;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PeterO.Cbor;
using Repo;
using Repo.MST;
using Sequencer.Db;
using Sequencer.Types;
using Util = Repo.Util;

namespace Sequencer;

public record CloseEvt;

public class SequencerRepository
{
    private readonly Crawlers _crawlers;

    private readonly ChannelWriter<Func<IServiceProvider, Task>> _backgroundJobWriter;
    private readonly SequencerDb _db;
    private readonly ILogger<SequencerRepository> _logger;

    public SequencerRepository(IDbContextFactory<SequencerDb> seqDbFactory, Crawlers crawlers, ChannelWriter<Func<IServiceProvider, Task>> backgroundJobWriter, ILogger<SequencerRepository> logger)
    {
        _db = seqDbFactory.CreateDbContext();
        _crawlers = crawlers;
        _backgroundJobWriter = backgroundJobWriter;
        _logger = logger;
    }

    public async Task<int?> CurrentAsync()
    {
        var seq = await _db.RepoSeqs
            .OrderByDescending(x => x.Seq)
            .FirstOrDefaultAsync();

        return seq?.Seq;
    }

    public async Task<RepoSeq?> NextAsync(int cursor)
    {
        var seq = await _db.RepoSeqs
            .Where(x => x.Seq > cursor)
            .OrderBy(x => x.Seq)
            .FirstOrDefaultAsync();

        return seq;
    }

    public async Task<RepoSeq?> EarliestAfterTimeAsync(DateTime time)
    {
        var seq = await _db.RepoSeqs
            .Where(x => x.SequencedAt > time)
            .OrderBy(x => x.Seq)
            .FirstOrDefaultAsync();

        return seq;
    }

    public async Task<ISeqEvt[]> GetRangeAsync(int? earliestSeq, int? latestSeq, DateTime? earliestTime, int? limit)
    {
        var seqs = _db.RepoSeqs.AsQueryable()
            .OrderBy(x => x.Seq)
            .Where(x => x.Invalidated == false);

        if (earliestSeq != null)
        {
            seqs = seqs.Where(x => x.Seq > earliestSeq);
        }

        if (latestSeq != null)
        {
            seqs = seqs.Where(x => x.Seq <= latestSeq);
        }

        if (earliestTime != null)
        {
            seqs = seqs.Where(x => x.SequencedAt >= earliestTime);
        }

        if (limit != null)
        {
            seqs = seqs.Take(limit.Value);
        }

        var rows = await seqs.ToArrayAsync();
        if (rows.Length < 1)
        {
            return [];
        }

        var seqEvents = new List<ISeqEvt>();
        foreach (var row in rows)
        {
            try
            {
                var evt = DecodeSeqEvent(row);
                if (evt != null) seqEvents.Add(evt);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error decoding event");
            }
        }

        return seqEvents.ToArray();
    }

    public static ISeqEvt? DecodeSeqEvent(RepoSeq row)
    {
        return row.EventType switch
        {
            RepoSeqEventType.Append or RepoSeqEventType.Rebase => new TypedCommitEvt
            {
                Seq = row.Seq,
                Time = row.SequencedAt,
                Evt = CommitEvt.FromCborObject(CBORObject.DecodeFromBytes(row.Event))
            },
            RepoSeqEventType.Handle => new TypedHandleEvt
            {
                Seq = row.Seq,
                Time = row.SequencedAt,
                Evt = HandleEvt.FromCborObject(CBORObject.DecodeFromBytes(row.Event))
            },
            RepoSeqEventType.Identity => new TypedIdentityEvt
            {
                Seq = row.Seq,
                Time = row.SequencedAt,
                Evt = IdentityEvt.FromCborObject(CBORObject.DecodeFromBytes(row.Event))
            },
            RepoSeqEventType.Account => new TypedAccountEvt
            {
                Seq = row.Seq,
                Time = row.SequencedAt,
                Evt = AccountEvt.FromCborObject(CBORObject.DecodeFromBytes(row.Event))
            },
            RepoSeqEventType.Tombstone => new TypedTombstoneEvt
            {
                Seq = row.Seq,
                Time = row.SequencedAt,
                Evt = TombstoneEvt.FromCborObject(CBORObject.DecodeFromBytes(row.Event))
            },
            _ => null
        };
    }

    public async Task DeleteAllForUserAsync(string did, int[] excludingSeq)
    {
        await _db.RepoSeqs
            .Where(x => x.Did == did && !excludingSeq.Contains(x.Seq))
            .ExecuteDeleteAsync();
    }

    public async Task<int> SequenceEventAsync(RepoSeq evt)
    {
        _db.RepoSeqs.Add(evt);
        await _db.SaveChangesAsync();
        var crawlers = _crawlers;
        await _backgroundJobWriter.WriteAsync(_ => crawlers.NotifyOfUpdateAsync());
        return evt.Seq;
    }

    public async Task<int> SequenceCommitAsync(string did, CommitData commitData, IPreparedWrite[] writes)
    {
        var evt = await FormatSeqCommitAsync(did, commitData, writes);
        return await SequenceEventAsync(evt);
    }

    public async Task<int> SequenceHandleUpdateAsync(string did, string handle)
    {
        var evt = FormatSeqHandleUpdate(did, handle);
        return await SequenceEventAsync(evt);
    }

    public async Task<int> SequenceIdentityEventAsync(string did, string? handle)
    {
        var evt = FormatSeqIdentityEvent(did, handle);
        return await SequenceEventAsync(evt);
    }

    public async Task<int> SequenceAccountEventAsync(string did, AccountStore.AccountStatus status)
    {
        var evt = FormatSeqAccountEvent(did, status);
        return await SequenceEventAsync(evt);
    }

    public async Task<int> SequenceTombstoneEventAsync(string did)
    {
        var evt = FormatSeqTombstoneEvent(did);
        return await SequenceEventAsync(evt);
    }

    private async Task<RepoSeq> FormatSeqCommitAsync(string did, CommitData commitData, IPreparedWrite[] writes)
    {
        var ops = new List<CommitEvtOp>();
        var blobs = new CidSet();
        bool tooBig;
        byte[] carSlice;
        // max too ops or 1mb of data
        if (writes.Length > 200 || commitData.NewBlocks.ByteSize > 1000000)
        {
            tooBig = true;
            var justRoot = new BlockMap();
            var rootBlock = commitData.NewBlocks.Get(commitData.Cid);
            if (rootBlock != null)
            {
                justRoot.Set(commitData.Cid, rootBlock);
            }

            carSlice = await Util.BlocksToCarFileAsync(commitData.Cid, justRoot);
        }
        else
        {
            tooBig = false;
            foreach (var w in writes)
            {
                var path = $"{w.Uri.Collection}/{w.Uri.RecordKey}";
                Cid? cid;
                if (w.Action == WriteOpAction.Delete)
                {
                    cid = null;
                }
                else
                {
                    var cpr = (IPreparedDataWrite)w;
                    cid = cpr.Cid;
                    foreach (var b in cpr.Blobs)
                    {
                        blobs.Add(b.Cid);
                    }
                }

                ops.Add(new CommitEvtOp
                {
                    Action = w.Action switch
                    {
                        WriteOpAction.Create => CommitEvtAction.Create,
                        WriteOpAction.Update => CommitEvtAction.Update,
                        WriteOpAction.Delete => CommitEvtAction.Delete,
                        _ => throw new ArgumentOutOfRangeException()
                    },
                    Cid = cid,
                    Path = path
                });
            }

            carSlice = await Util.BlocksToCarFileAsync(commitData.Cid, commitData.NewBlocks);
        }

        return new RepoSeq
        {
            SequencedAt = DateTime.UtcNow,
            Event = new CommitEvt
            {
                Rebase = false,
                TooBig = tooBig,
                Repo = did,
                Commit = commitData.Cid,
                Prev = commitData.Prev,
                Rev = commitData.Rev,
                Since = commitData.Since,
                Blocks = carSlice,
                Ops = ops.ToArray(),
                Blobs = blobs.ToArray()
            }.ToCborObject().EncodeToBytes(),
            EventType = RepoSeqEventType.Append,
            Did = did
        };
    }

    private RepoSeq FormatSeqHandleUpdate(string did, string handle)
    {
        var handleEvt = new HandleEvt
        {
            Did = did,
            Handle = handle
        };

        return new RepoSeq
        {
            Did = did,
            EventType = RepoSeqEventType.Handle,
            Event = handleEvt.ToCborObject().EncodeToBytes(),
            SequencedAt = DateTime.UtcNow
        };
    }

    private RepoSeq FormatSeqIdentityEvent(string did, string? handle)
    {
        var identityEvt = new IdentityEvt
        {
            Did = did,
            Handle = handle
        };

        return new RepoSeq
        {
            Did = did,
            EventType = RepoSeqEventType.Identity,
            Event = identityEvt.ToCborObject().EncodeToBytes(),
            SequencedAt = DateTime.UtcNow
        };
    }

    private RepoSeq FormatSeqAccountEvent(string did, AccountStore.AccountStatus status)
    {
        var accountEvt = new AccountEvt
        {
            Did = did,
            Active = status == AccountStore.AccountStatus.Active,
            Status = status == AccountStore.AccountStatus.Active ? null : status
        };

        return new RepoSeq
        {
            Did = did,
            EventType = RepoSeqEventType.Account,
            Event = accountEvt.ToCborObject().EncodeToBytes(),
            SequencedAt = DateTime.UtcNow
        };
    }

    private RepoSeq FormatSeqTombstoneEvent(string did)
    {
        var tombstoneEvt = new TombstoneEvt
        {
            Did = did
        };

        return new RepoSeq
        {
            Did = did,
            EventType = RepoSeqEventType.Tombstone,
            Event = tombstoneEvt.ToCborObject().EncodeToBytes(),
            SequencedAt = DateTime.UtcNow
        };
    }
}
