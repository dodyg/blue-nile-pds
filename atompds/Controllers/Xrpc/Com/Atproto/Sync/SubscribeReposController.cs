using System.Runtime.CompilerServices;
using Config;
using Microsoft.AspNetCore.Mvc;
using Sequencer;
using Sequencer.Types;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Sync;

[ApiController]
[Route("xrpc")]
public class SubscribeReposController : ControllerBase
{
    private readonly SubscriptionConfig _subscriptionConfig;
    private readonly SequencerRepository _sequencer;
    public SubscribeReposController(SubscriptionConfig subscriptionConfig, SequencerRepository sequencer)
    {
        _subscriptionConfig = subscriptionConfig;
        _sequencer = sequencer;
    }
    
    [HttpGet("com.atproto.sync.subscribeRepos")]
    public async IAsyncEnumerable<object> SubscribeRepos(
        [FromQuery] int? cursor, // The last known event seq number to backfill from.
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var outbox = new Outbox(_sequencer, new OutboxOpts(_subscriptionConfig.MaxSubscriptionBuffer));
        var backfillTime = DateTime.UtcNow - TimeSpan.FromMilliseconds(_subscriptionConfig.RepoBackfillLimitMs);
        int? outboxCursor = null;
        if (cursor != null)
        {
            var next = await _sequencer.Next(cursor.Value);
            var curr = await _sequencer.Current();
            if (cursor.Value > (curr ?? 0))
            {
                throw new XRPCError(new InvalidRequestErrorDetail("FutureCursor", "Cursor in the future."));
            }
            else if (next != null && next.SequencedAt < backfillTime)
            {
                yield return new Dictionary<string, string>
                {
                    {"$type", "#info"},
                    {"name", "OutDatedCursor"},
                    {"message", "Requested cursor exceeded limit. Possibly missing events."}
                };
                var startEvt = await _sequencer.EarliestAfterTime(backfillTime);
                outboxCursor = startEvt?.Seq - 1;
            }
            else
            {
                outboxCursor = cursor;
            }
        }
        
        await foreach (var evt in outbox.Events(outboxCursor, cancellationToken))
        {
            if (evt.Type == TypedCommitType.Commit && evt is TypedCommitEvt commit)
            {
                // flatten the commit event
                var outJson = commit.Evt.ToCborObject()
                    .Add("$type", "#commit")
                    .Add("seq", evt.Seq)
                    .Add("time", evt.Time.ToString("O"))
                    .ToJSONString();
                
                yield return outJson;
            }
            else if (evt.Type == TypedCommitType.Handle && evt is TypedHandleEvt handle)
            {
                // flatten the handle event
                var outJson = handle.Evt.ToCborObject()
                    .Add("$type", "#handle")
                    .Add("seq", evt.Seq)
                    .Add("time", evt.Time.ToString("O"))
                    .ToJSONString();
                
                yield return outJson;
            }
            else if (evt.Type == TypedCommitType.Account && evt is TypedAccountEvt account)
            {
                // flatten the account event
                var outJson = account.Evt.ToCborObject()
                    .Add("$type", "#account")
                    .Add("seq", evt.Seq)
                    .Add("time", evt.Time.ToString("O"))
                    .ToJSONString();
                
                yield return outJson;
            }
            else if (evt.Type == TypedCommitType.Tombstone && evt is TypedTombstoneEvt tomestoneEvt)
            {
                // flatten the event
                var outJson = tomestoneEvt.Evt.ToCborObject()
                    .Add("$type", "#tombstone")
                    .Add("seq", evt.Seq)
                    .Add("time", evt.Time.ToString("O"))
                    .ToJSONString();
                
                yield return outJson;
            }
        }
    }
}