using System.Net.WebSockets;
using System.Text;
using Config;
using FishyFlip.Models;
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
    private readonly SubscriptionConfig _subscriptionConfig;
    private readonly SequencerRepository _sequencer;
    private readonly ILogger<SubscribeReposController> _logger;
    public SubscribeReposController(SubscriptionConfig subscriptionConfig, SequencerRepository sequencer,
        ILogger<SubscribeReposController> logger)
    {
        _subscriptionConfig = subscriptionConfig;
        _sequencer = sequencer;
        _logger = logger;
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
        var cborOpts = new CBOREncodeOptions("useIndefLengthStrings=true;float64=true;allowduplicatekeys=true;allowEmpty=true");
        try
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
                    var header = CBORObject.NewMap()
                        .Add("t", "#info")
                        .Add("op", FrameHeaderOperation.Error)
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
                        .Add("op", FrameHeaderOperation.Error)
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
                        .Add("op", FrameHeaderOperation.Frame)
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
                        .Add("op", FrameHeaderOperation.Frame)
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
                        .Add("op", FrameHeaderOperation.Frame)
                        .EncodeToBytes(cborOpts);
                    var blob = account.Evt.ToCborObject()
                        .Add("seq", evt.Seq)
                        .Add("time", evt.Time.ToString("O"))
                        .EncodeToBytes(cborOpts);
                    byte[] buffer = [..header, ..blob];
                    await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
                }
                else if (evt.Type == TypedCommitType.Tombstone && evt is TypedTombstoneEvt tomestoneEvt)
                {
                    var header = CBORObject.NewMap()
                        .Add("t", "#tombstone")
                        .Add("op", FrameHeaderOperation.Frame)
                        .EncodeToBytes(cborOpts);
                    var blob = tomestoneEvt.Evt.ToCborObject()
                        .Add("seq", evt.Seq)
                        .Add("time", evt.Time.ToString("O"))
                        .EncodeToBytes(cborOpts);
                    byte[] buffer = [..header, ..blob];
                    await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Subscription error.");
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Subscription cancelled.");
            }
            else
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