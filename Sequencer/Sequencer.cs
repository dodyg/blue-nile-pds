using AccountManager.Db;
using CID;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PeterO.Cbor;
using Repo;
using Repo.MST;
using Sequencer.Db;
using Sequencer.Types;

namespace Sequencer;

public record CloseEvt;

public class SequencerRepository : IDisposable
{
    public event EventHandler<ISeqEvt[]>? OnEvents;
    public event EventHandler<CloseEvt>? OnClose;
    
    private readonly SequencerDb _db;
    private readonly Crawlers _crawlers;
    private readonly ILogger<SequencerRepository> _logger;
    private readonly CancellationTokenSource _cts = new();
    public int? LastSeen { get; private set; }
    private int TriesWithNoResults { get; set; }
    private Task _pollTask = null!;
    
    public SequencerRepository(SequencerDb db, Crawlers crawlers, ILogger<SequencerRepository> logger)
    {
        _db = db;
        _crawlers = crawlers;
        _logger = logger;
    }

    private async Task PollTask()
    {
        while (_cts.Token.IsCancellationRequested == false)
        {
            try
            {
                await PollDb();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error polling db");
            }
        }
    }

    private async Task PollDb()
    {
        try
        {
            var evts = await GetRange(LastSeen, null, null, 1000);
            if (evts.Length > 0)
            {
                TriesWithNoResults = 0;
                OnEvents?.Invoke(this, evts);
                LastSeen = evts.Max(x => x.Seq);
            }
            else
            {
                await ExponentialBackoff();
            }
        }
        catch (Exception)
        {
            await ExponentialBackoff();
        }
    }

    private async Task ExponentialBackoff()
    {
        TriesWithNoResults++;
        var delay = Math.Pow(2, TriesWithNoResults);
        var delayLength = Math.Min(1000, (int)delay);
        await Task.Delay(delayLength, _cts.Token);
    }

    public async Task<int?> Current()
    {
        var seq = await _db.RepoSeqs
            .OrderByDescending(x => x.Seq)
            .FirstOrDefaultAsync();
        
        return seq?.Seq;
    }

    public async Task<RepoSeq?> Next(int cursor)
    {
        var seq = await _db.RepoSeqs
            .Where(x => x.Seq > cursor)
            .OrderBy(x => x.Seq)
            .FirstOrDefaultAsync();
        
        return seq;
    }
    
    public async Task<RepoSeq?> EarliestAfterTime(DateTime time)
    {
        var seq = await _db.RepoSeqs
            .Where(x => x.SequencedAt > time)
            .OrderBy(x => x.Seq)
            .FirstOrDefaultAsync();
        
        return seq;
    }

    public async Task<ISeqEvt[]> GetRange(int? earliestSeq, int? latestSeq, DateTime? earliestTime, int? limit)
    {
        var seqs = _db.RepoSeqs.AsQueryable()
            .OrderBy(x => x.Seq)
            .Where(x => x.Invalidated == false);

        if (earliestSeq != null)
        {
            seqs = seqs.Where(x => x.Seq >= earliestSeq);
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
            switch (row.EventType)
            {

                case RepoSeqEventType.Append:
                case RepoSeqEventType.Rebase:
                    var commitEvt = CommitEvt.FromCborObject(CBORObject.DecodeFromBytes(row.Event));
                    seqEvents.Add(new TypedCommitEvt
                    {
                        Seq = row.Seq,
                        Time = row.SequencedAt,
                        Evt = commitEvt
                    });
                    break;
                case RepoSeqEventType.Handle:
                    var handleEvt = HandleEvt.FromCborObject(CBORObject.DecodeFromBytes(row.Event));
                    seqEvents.Add(new TypedHandleEvt
                    {
                        Seq = row.Seq,
                        Time = row.SequencedAt,
                        Evt = handleEvt
                    });
                    break;
                case RepoSeqEventType.Identity:
                    var identityEvt = IdentityEvt.FromCborObject(CBORObject.DecodeFromBytes(row.Event));
                    seqEvents.Add(new TypedIdentityEvt
                    {
                        Seq = row.Seq,
                        Time = row.SequencedAt,
                        Evt = identityEvt
                    });
                    break;
                case RepoSeqEventType.Account:
                    var accountEvt = AccountEvt.FromCborObject(CBORObject.DecodeFromBytes(row.Event));
                    seqEvents.Add(new TypedAccountEvt
                    {
                        Seq = row.Seq,
                        Time = row.SequencedAt,
                        Evt = accountEvt
                    });
                    break;
                case RepoSeqEventType.Tombstone:
                    var tombstoneEvt = TombstoneEvt.FromCborObject(CBORObject.DecodeFromBytes(row.Event));
                    seqEvents.Add(new TypedTombstoneEvt
                    {
                        Seq = row.Seq,
                        Time = row.SequencedAt,
                        Evt = tombstoneEvt
                    });
                    break;
            }
        }
        
        return seqEvents.ToArray();
    }

    public async Task<int> SequenceEvent(RepoSeq evt)
    {
        _db.RepoSeqs.Add(evt);
        await _db.SaveChangesAsync();
        await _crawlers.NotifyOfUpdate();
        return evt.Seq;
    }

    public async Task<int> SequenceCommit(string did, CommitData commitData, IPreparedWrite[] writes)
    {
        var evt = await FormatSeqCommit(did, commitData, writes);
        return await SequenceEvent(evt);
    }
    
    public async Task<int> SequenceHandleUpdate(string did, string handle)
    {
        var evt = FormatSeqHandleUpdate(did, handle);
        return await SequenceEvent(evt);
    }
    
    public async Task<int> SequenceIdentityEvent(string did, string? handle)
    {
        var evt = FormatSeqIdentityEvent(did, handle);
        return await SequenceEvent(evt);
    }
    
    public async Task<int> SequenceAccountEvent(string did, AccountStore.AccountStatus status)
    {
        var evt = FormatSeqAccountEvent(did, status);
        return await SequenceEvent(evt);
    }
    
    public async Task<int> SequenceTombstoneEvent(string did)
    {
        var evt = FormatSeqTombstoneEvent(did);
        return await SequenceEvent(evt);
    }
    
    private async Task<RepoSeq> FormatSeqCommit(string did, CommitData commitData, IPreparedWrite[] writes)
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

            carSlice = await Repo.Util.BlocksToCarFile(commitData.Cid, justRoot);
        }
        else
        {
            tooBig = false;
            foreach (var w in writes)
            {
                var path = $"{w.Uri.Collection}/{w.Uri.Rkey}";
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

            carSlice = await Repo.Util.BlocksToCarFile(commitData.Cid, commitData.NewBlocks);
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
            Status = status == AccountStore.AccountStatus.Active ? null : status,
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
    
    public void Dispose()
    {
        _cts.Cancel();
        _pollTask?.Wait();
        OnClose?.Invoke(this, new CloseEvt());
        _db.Dispose();
    }
    
    public async Task DeleteAllForUser(string did, int[] excludingSeq)
    {
        await _db.RepoSeqs
            .Where(x => x.Did == did && !excludingSeq.Contains(x.Seq))
            .ExecuteDeleteAsync();
    }
}