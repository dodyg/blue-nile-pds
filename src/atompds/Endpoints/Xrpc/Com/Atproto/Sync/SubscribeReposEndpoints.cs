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
        ILogger<Program> logger,
        ILoggerFactory loggerFactory,
        int? cursor,
        CancellationToken cancellationToken)
    {
        if (!context.WebSockets.IsWebSocketRequest)
            throw new XRPCError(new InvalidRequestErrorDetail("NotWebSocket", "Request is not a websocket."));

        logger.LogInformation("Subscribing to repos, cursor: {Cursor}", cursor);
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var cborOpts = CBOREncodeOptions.Default;
        try
        {
            var outbox = new Outbox(sequencer, new OutboxOpts(subscriptionConfig.MaxSubscriptionBuffer), loggerFactory.CreateLogger<Outbox>());
            var backfillTime = DateTime.UtcNow - TimeSpan.FromMilliseconds(subscriptionConfig.RepoBackfillLimitMs);
            int? outboxCursor = null;
            if (cursor != null)
            {
                var next = await sequencer.NextAsync(cursor.Value);
                var curr = await sequencer.CurrentAsync();
                if (cursor.Value > (curr ?? 0))
                {
                    var header = CBORObject.NewMap().Add("t", "#info").Add("op", ErrorOperation).EncodeToBytes();
                    var blob = CBORObject.NewMap().Add("atError", "FutureCursor").Add("message", "Requested cursor is in the future.").EncodeToBytes();
                    byte[] buffer = [..header, ..blob];
                    await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
                    logger.LogInformation("Future cursor requested.");
                    return;
                }

                if (next != null && next.SequencedAt < backfillTime)
                {
                    var header = CBORObject.NewMap().Add("t", "#info").Add("op", ErrorOperation).EncodeToBytes();
                    var blob = CBORObject.NewMap().Add("atError", "OutdatedCursor").Add("message", "Requested cursor exceeded limit. Possibly missing events.").EncodeToBytes();
                    byte[] buffer = [..header, ..blob];
                    await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
                    var startEvt = await sequencer.EarliestAfterTimeAsync(backfillTime);
                    outboxCursor = startEvt?.Seq - 1;
                }
                else
                {
                    outboxCursor = cursor;
                }
            }

            await foreach (var evt in outbox.EventsAsync(outboxCursor, cancellationToken, webSocket))
            {
                if (evt.Type == TypedCommitType.Commit && evt is TypedCommitEvt commit)
                {
                    var header = CBORObject.NewMap().Add("t", "#commit").Add("op", FrameOperation).EncodeToBytes(cborOpts);
                    var evtBlob = commit.Evt.ToCborObject().Add("seq", evt.Seq).Add("time", evt.Time.ToString("O"));
                    byte[] buffer = [..header, ..evtBlob.EncodeToBytes(cborOpts)];
                    await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
                }
                else if (evt.Type == TypedCommitType.Handle && evt is TypedHandleEvt handle)
                {
                    var header = CBORObject.NewMap().Add("t", "#handle").Add("op", FrameOperation).EncodeToBytes(cborOpts);
                    var evtBlob = handle.Evt.ToCborObject().Add("seq", evt.Seq).Add("time", evt.Time.ToString("O")).EncodeToBytes(cborOpts);
                    byte[] buffer = [..header, ..evtBlob];
                    await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
                }
                else if (evt.Type == TypedCommitType.Account && evt is TypedAccountEvt account)
                {
                    var header = CBORObject.NewMap().Add("t", "#account").Add("op", FrameOperation).EncodeToBytes(cborOpts);
                    var evtBlob = account.Evt.ToCborObject().Add("seq", evt.Seq).Add("time", evt.Time.ToString("O")).EncodeToBytes(cborOpts);
                    byte[] buffer = [..header, ..evtBlob];
                    await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
                }
                else if (evt.Type == TypedCommitType.Tombstone && evt is TypedTombstoneEvt tombstone)
                {
                    var header = CBORObject.NewMap().Add("t", "#tombstone").Add("op", FrameOperation).EncodeToBytes(cborOpts);
                    var evtBlob = tombstone.Evt.ToCborObject().Add("seq", evt.Seq).Add("time", evt.Time.ToString("O")).EncodeToBytes(cborOpts);
                    byte[] buffer = [..header, ..evtBlob];
                    await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Subscription cancelled by client.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Subscription error.");
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
                logger.LogInformation("Subscription ended.");

            if (!webSocket.CloseStatus.HasValue)
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Subscription ended.", cancellationToken);
        }
    }
}
