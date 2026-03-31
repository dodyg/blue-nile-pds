using System.Net.WebSockets;
using Config;
using Microsoft.AspNetCore.Mvc;
using PeterO.Cbor;
using Sequencer;
using Sequencer.Types;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Sync;

[ApiController]
[Route("xrpc")]
public class SubscribeReposController : ControllerBase
{
    private const int FrameOperation = 1;
    private const int ErrorOperation = -1;

    private readonly ILogger<SubscribeReposController> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly SequencerRepository _sequencer;
    private readonly SubscriptionConfig _subscriptionConfig;
    public SubscribeReposController(SubscriptionConfig subscriptionConfig,
        SequencerRepository sequencer,
        ILogger<SubscribeReposController> logger,
        ILoggerFactory loggerFactory)
    {
        _subscriptionConfig = subscriptionConfig;
        _sequencer = sequencer;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    [HttpGet("com.atproto.sync.subscribeRepos")]
    public async Task SubscribeRepos(
        [FromQuery] int? cursor, // The last known event seq number to backfill from.
        CancellationToken cancellationToken)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("NotWebSocket", "Request is not a websocket."));
        }

        _logger.LogInformation("Subscribing to repos, cursor: {Cursor}", cursor);
        using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        var cborOpts = CBOREncodeOptions.Default;
        try
        {
            var outbox = new Outbox(_sequencer, new OutboxOpts(_subscriptionConfig.MaxSubscriptionBuffer), _loggerFactory.CreateLogger<Outbox>());
            var backfillTime = DateTime.UtcNow - TimeSpan.FromMilliseconds(_subscriptionConfig.RepoBackfillLimitMs);
            int? outboxCursor = null;
            if (cursor != null)
            {
                var next = await _sequencer.Next(cursor.Value);
                var curr = await _sequencer.Current();
                if (cursor.Value > (curr ?? 0))
                {
                    var header = CBORObject.NewMap()
                        .Add("t", "#info")
                        .Add("op", ErrorOperation)
                        .EncodeToBytes();
                    var blob = CBORObject.NewMap()
                        .Add("atError", "FutureCursor")
                        .Add("message", "Requested cursor is in the future.")
                        .EncodeToBytes();
                    byte[] buffer = [..header, ..blob];

                    await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
                    _logger.LogInformation("Future cursor requested.");

                    return;
                }

                if (next != null && next.SequencedAt < backfillTime)
                {
                    var header = CBORObject.NewMap()
                        .Add("t", "#info")
                        .Add("op", ErrorOperation)
                        .EncodeToBytes();
                    var blob = CBORObject.NewMap()
                        .Add("atError", "OutdatedCursor")
                        .Add("message", "Requested cursor exceeded limit. Possibly missing events.")
                        .EncodeToBytes();
                    byte[] buffer = [..header, ..blob];

                    await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
                    var startEvt = await _sequencer.EarliestAfterTime(backfillTime);
                    outboxCursor = startEvt?.Seq - 1;
                }
                else
                {
                    outboxCursor = cursor;
                }
            }

            await foreach (var evt in outbox.Events(outboxCursor, cancellationToken, webSocket))
            {
                //_logger.LogInformation("Handling {type} event: {Seq}", evt.Type, evt.Seq);
                if (evt.Type == TypedCommitType.Commit && evt is TypedCommitEvt commit)
                {
                    var header = CBORObject.NewMap()
                        .Add("t", "#commit")
                        .Add("op", FrameOperation)
                        .EncodeToBytes(cborOpts);
                    var blob = commit.Evt.ToCborObject()
                        .Add("seq", evt.Seq)
                        .Add("time", evt.Time.ToString("O"));
                    byte[] buffer = [..header, ..blob.EncodeToBytes(cborOpts)];
                    await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
                }
                else if (evt.Type == TypedCommitType.Handle && evt is TypedHandleEvt handle)
                {
                    var header = CBORObject.NewMap()
                        .Add("t", "#handle")
                        .Add("op", FrameOperation)
                        .EncodeToBytes(cborOpts);
                    var blob = handle.Evt.ToCborObject()
                        .Add("seq", evt.Seq)
                        .Add("time", evt.Time.ToString("O"))
                        .EncodeToBytes(cborOpts);
                    byte[] buffer = [..header, ..blob];
                    await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
                }
                else if (evt.Type == TypedCommitType.Account && evt is TypedAccountEvt account)
                {
                    var header = CBORObject.NewMap()
                        .Add("t", "#account")
                        .Add("op", FrameOperation)
                        .EncodeToBytes(cborOpts);
                    var blob = account.Evt.ToCborObject()
                        .Add("seq", evt.Seq)
                        .Add("time", evt.Time.ToString("O"))
                        .EncodeToBytes(cborOpts);
                    byte[] buffer = [..header, ..blob];
                    await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
                }
                else if (evt.Type == TypedCommitType.Tombstone && evt is TypedTombstoneEvt tombstone)
                {
                    var header = CBORObject.NewMap()
                        .Add("t", "#tombstone")
                        .Add("op", FrameOperation)
                        .EncodeToBytes(cborOpts);
                    var blob = tombstone.Evt.ToCborObject()
                        .Add("seq", evt.Seq)
                        .Add("time", evt.Time.ToString("O"))
                        .EncodeToBytes(cborOpts);
                    byte[] buffer = [..header, ..blob];
                    await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Subscription cancelled by client.");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Subscription error.");
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Subscription ended.");
            }

            if (!webSocket.CloseStatus.HasValue)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Subscription ended.", cancellationToken);
            }
        }
    }
}
