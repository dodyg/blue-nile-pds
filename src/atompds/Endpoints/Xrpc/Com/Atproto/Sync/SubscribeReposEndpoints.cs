using System.Net.WebSockets;
using Config;
using PeterO.Cbor;
using Sequencer;
using Sequencer.Types;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Sync;

public static class SubscribeReposEndpoints
{
    private const int FrameOperation = 1;
    private const int ErrorOperation = -1;

    public static RouteGroupBuilder MapSubscribeReposEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.sync.subscribeRepos", HandleAsync);
        return group;
    }

    private static async Task HandleAsync(
        HttpContext context,
        SubscriptionConfig subscriptionConfig,
        SequencerRepository sequencer,
        Sequencer.ISequencerEventSource eventSource,
        ILogger<Program> logger,
        ILoggerFactory loggerFactory,
        int? cursor,
        CancellationToken cancellationToken)
    {
        if (!context.WebSockets.IsWebSocketRequest)
            throw new XRPCError(new InvalidRequestErrorDetail("NotWebSocket", "Request is not a websocket."));

        logger.LogInformation("WebSocket subscription request received, cursor: {Cursor}", cursor);
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        logger.LogInformation("WebSocket upgraded for subscription");
        var cborOpts = CBOREncodeOptions.Default;
        var totalSent = 0;
        try
        {
            var outbox = new Outbox(sequencer, eventSource, new OutboxOpts(subscriptionConfig.MaxSubscriptionBuffer), loggerFactory.CreateLogger<Outbox>());
            var backfillTime = DateTime.UtcNow - TimeSpan.FromMilliseconds(subscriptionConfig.RepoBackfillLimitMs);
            int? outboxCursor = null;
            if (cursor != null)
            {
                logger.LogInformation("Cursor provided: {Cursor}, backfill cut-off: {BackfillTime}", cursor, backfillTime);
                var next = await sequencer.NextAsync(cursor.Value);
                var curr = await sequencer.CurrentAsync();
                if (cursor.Value > (curr ?? 0))
                {
                    logger.LogWarning("Future cursor requested: {Cursor} > current max {Current}", cursor, curr);
                    var header = CBORObject.NewMap().Add("t", "#info").Add("op", ErrorOperation).EncodeToBytes();
                    var blob = CBORObject.NewMap().Add("error", "FutureCursor").Add("message", "Requested cursor is in the future.").EncodeToBytes();
                    byte[] buffer = [.. header, .. blob];
                    await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
                    return;
                }

                if (next != null && next.SequencedAt < backfillTime)
                {
                    logger.LogWarning("Outdated cursor: next seq {NextSeq} at {SequencedAt} is before cut-off {BackfillTime}", next.Seq, next.SequencedAt, backfillTime);
                    var header = CBORObject.NewMap().Add("t", "#info").Add("op", FrameOperation).EncodeToBytes();
                    var blob = CBORObject.NewMap().Add("name", "OutdatedCursor").Add("message", "Requested cursor exceeded limit. Possibly missing events.").EncodeToBytes();
                    byte[] buffer = [.. header, .. blob];
                    await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
                    var startEvt = await sequencer.EarliestAfterTimeAsync(backfillTime);
                    outboxCursor = startEvt?.Seq - 1;
                    logger.LogInformation("Adjusted outdated cursor to {OutboxCursor} (startEvt seq: {StartSeq})", outboxCursor, startEvt?.Seq);
                }
                else
                {
                    outboxCursor = cursor;
                    logger.LogInformation("Using provided cursor {Cursor} for backfill", cursor);
                }
            }
            else
            {
                logger.LogInformation("No cursor provided, will backfill from seq 0");
            }

            await foreach (var evt in outbox.EventsAsync(outboxCursor, cancellationToken, webSocket))
            {
                if (evt.Type == TypedCommitType.Commit && evt is TypedCommitEvt commit)
                {
                    var header = CBORObject.NewMap().Add("t", "#commit").Add("op", FrameOperation).EncodeToBytes(cborOpts);
                    var evtBlob = commit.Evt.ToCborObject().Add("seq", evt.Seq).Add("time", evt.Time.ToString("O"));
                    byte[] buffer = [.. header, .. evtBlob.EncodeToBytes(cborOpts)];
                    await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
                    logger.LogDebug("Sent commit event seq {Seq} for {Repo}", evt.Seq, commit.Evt.Repo);
                }
                else if (evt.Type == TypedCommitType.Handle && evt is TypedHandleEvt handle)
                {
                    var header = CBORObject.NewMap().Add("t", "#handle").Add("op", FrameOperation).EncodeToBytes(cborOpts);
                    var evtBlob = handle.Evt.ToCborObject().Add("seq", evt.Seq).Add("time", evt.Time.ToString("O")).EncodeToBytes(cborOpts);
                    byte[] buffer = [.. header, .. evtBlob];
                    await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
                    logger.LogDebug("Sent handle event seq {Seq} for {Repo}", evt.Seq, handle.Evt.Did);
                }
                else if (evt.Type == TypedCommitType.Account && evt is TypedAccountEvt account)
                {
                    var header = CBORObject.NewMap().Add("t", "#account").Add("op", FrameOperation).EncodeToBytes(cborOpts);
                    var evtBlob = account.Evt.ToCborObject().Add("seq", evt.Seq).Add("time", evt.Time.ToString("O")).EncodeToBytes(cborOpts);
                    byte[] buffer = [.. header, .. evtBlob];
                    await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
                    logger.LogDebug("Sent account event seq {Seq} for {Repo}, active: {Active}", evt.Seq, account.Evt.Did, account.Evt.Active);
                }
                else if (evt.Type == TypedCommitType.Tombstone && evt is TypedTombstoneEvt tombstone)
                {
                    var header = CBORObject.NewMap().Add("t", "#tombstone").Add("op", FrameOperation).EncodeToBytes(cborOpts);
                    var evtBlob = tombstone.Evt.ToCborObject().Add("seq", evt.Seq).Add("time", evt.Time.ToString("O")).EncodeToBytes(cborOpts);
                    byte[] buffer = [.. header, .. evtBlob];
                    await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
                    logger.LogDebug("Sent tombstone event seq {Seq} for {Repo}", evt.Seq, tombstone.Evt.Did);
                }
                totalSent++;
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Subscription cancelled by client after sending {TotalSent} events", totalSent);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Subscription error after sending {TotalSent} events", totalSent);
        }
        finally
        {
            logger.LogInformation("Subscription ended, sent {TotalSent} events total", totalSent);

            if (!webSocket.CloseStatus.HasValue)
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Subscription ended.", cancellationToken);
        }
    }
}
