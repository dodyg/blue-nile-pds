using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Sequencer.Types;
using Xrpc;

namespace Sequencer;

public interface ISequencerEventSource
{
    event EventHandler<ISeqEvt[]> OnEvents;
}

public record OutboxOpts(int MaxBufferSize);

public class Outbox
{
    private readonly ILogger<Outbox> _logger;
    private readonly OutboxOpts _opts;
    private readonly SequencerRepository _sequencer;
    private readonly ISequencerEventSource _eventSource;
    private volatile bool _caughtUp;
    private readonly object _cutoverLock = new();

    public Outbox(SequencerRepository sequencer, ISequencerEventSource eventSource, OutboxOpts opts, ILogger<Outbox> logger)
    {
        _sequencer = sequencer;
        _eventSource = eventSource;
        _opts = opts;
        _logger = logger;
        OutBuffer = Channel.CreateBounded<ISeqEvt>(opts.MaxBufferSize);
    }
    public int LastSeen { get; private set; } = -1;
    public Channel<ISeqEvt> OutBuffer { get; private init; }
    public ConcurrentQueue<ISeqEvt> CutoverBuffer { get; } = new();

    public async IAsyncEnumerable<ISeqEvt> EventsAsync(int? backfillCursor, [EnumeratorCancellation] CancellationToken token, WebSocket webSocket)
    {
        var startCursor = backfillCursor;
        _logger.LogInformation("EventsAsync started, cursor: {Cursor}", backfillCursor);

        if (backfillCursor != null)
        {
            var backfillCount = 0;
            await foreach (var evt in GetBackfillAsync(backfillCursor.Value, token))
            {
                if (token.IsCancellationRequested)
                {
                    _logger.LogInformation("Backfill cancelled after {Count} events", backfillCount);
                    yield break;
                }
                backfillCount++;
                LastSeen = evt.Seq;
                _logger.LogDebug("Backfill yielded event seq {Seq} (total: {Count})", evt.Seq, backfillCount);
                yield return evt;
            }
            _logger.LogInformation("Backfill complete: yielded {Count} events, LastSeen={LastSeen}", backfillCount, LastSeen);
        }
        else
        {
            _logger.LogInformation("No cursor, backfilling from seq 0");
            var backfillCount = 0;
            await foreach (var evt in GetBackfillAsync(0, token))
            {
                if (token.IsCancellationRequested)
                {
                    _logger.LogInformation("Backfill cancelled after {Count} events", backfillCount);
                    yield break;
                }
                backfillCount++;
                LastSeen = evt.Seq;
                _logger.LogDebug("Backfill yielded event seq {Seq} (total: {Count})", evt.Seq, backfillCount);
                yield return evt;
            }
            _logger.LogInformation("Full backfill complete: yielded {Count} events, LastSeen={LastSeen}", backfillCount, LastSeen);
        }

        _logger.LogInformation("Subscribing to OnEvents, LastSeen={LastSeen}", LastSeen);
        _eventSource.OnEvents += OnEvents;

        var liveCount = 0;
        var cutoverCount = 0;
        try
        {
            _logger.LogInformation("Running cutover, backfillCursor: {Cursor}, LastSeen: {LastSeen}", backfillCursor, LastSeen);
            var cutoverEvents = await CollectCutoverAsync(backfillCursor);
            _logger.LogInformation("Cutover complete, collected {Count} events, LastSeen={LastSeen}, CutoverBuffer count: {BufCount}", cutoverEvents.Count, LastSeen, CutoverBuffer.Count);

            foreach (var evt in cutoverEvents)
            {
                if (evt.Seq > LastSeen)
                {
                    cutoverCount++;
                    LastSeen = evt.Seq;
                    yield return evt;
                }
            }
            _logger.LogInformation("Cutover yielded {Count} events, LastSeen={LastSeen}", cutoverCount, LastSeen);

            _logger.LogInformation("Entering live event loop, LastSeen={LastSeen}", LastSeen);
            await foreach (var evt in OutBuffer.Reader.ReadAllAsync(token))
            {
                if (webSocket.State != WebSocketState.Open)
                {
                    _logger.LogWarning("WebSocket no longer open, exiting live loop after {Count} live events", liveCount);
                    yield break;
                }
                if (evt.Seq > LastSeen)
                {
                    liveCount++;
                    _logger.LogDebug("Live event seq {Seq} (total live: {Count})", evt.Seq, liveCount);
                    LastSeen = evt.Seq;
                    yield return evt;
                }
                else
                {
                    _logger.LogDebug("Skipping duplicate event seq {Seq} (LastSeen={LastSeen})", evt.Seq, LastSeen);
                }
            }
            _logger.LogInformation("Live event loop exited after {Count} events", liveCount);
        }
        finally
        {
            _eventSource.OnEvents -= OnEvents;
            _logger.LogInformation("Unsubscribed from OnEvents, total live events: {Count}, total cutover events: {CutoverCount}", liveCount, cutoverCount);
        }
    }

    private async Task<List<ISeqEvt>> CollectCutoverAsync(int? backfillCursor)
    {
        if (backfillCursor == null)
        {
            _logger.LogInformation("CollectCutover: no backfill cursor, setting caughtUp=true");
            _caughtUp = true;
            return [];
        }

        var result = new List<ISeqEvt>();

        var cursor = LastSeen > -1 ? LastSeen : backfillCursor.Value;
        _logger.LogInformation("CollectCutover: querying events after LastSeen={LastSeen} (cursor={Cursor})", LastSeen, cursor);
        var dbEvents = await _sequencer.GetRangeAsync(cursor, null, null, null);
        _logger.LogInformation("CollectCutover: found {Count} events from DB, CutoverBuffer has {BufCount}", dbEvents.Length, CutoverBuffer.Count);
        result.AddRange(dbEvents);

        lock (_cutoverLock)
        {
            _logger.LogInformation("CollectCutover: flushing {Count} buffered events from CutoverBuffer", CutoverBuffer.Count);
            while (CutoverBuffer.TryDequeue(out var evt))
            {
                result.Add(evt);
            }
            _caughtUp = true;
        }

        result.Sort((a, b) => a.Seq.CompareTo(b.Seq));
        _logger.LogInformation("CollectCutover: collected {Count} total events, LastSeen={LastSeen}", result.Count, LastSeen);
        return result;
    }

    public async IAsyncEnumerable<ISeqEvt> GetBackfillAsync(int backfillCursor, [EnumeratorCancellation] CancellationToken token)
    {
        const int PAGE_SIZE = 500;
        var totalPageCount = 0;
        var totalEventCount = 0;
        while (true)
        {
            if (token.IsCancellationRequested)
            {
                _logger.LogInformation("Backfill cancelled on page {Page}", totalPageCount + 1);
                yield break;
            }

            var queryCursor = LastSeen > -1 ? LastSeen : backfillCursor;
            _logger.LogDebug("Backfill page {Page}: querying events after seq {Cursor}", totalPageCount + 1, queryCursor);
            var evts = await _sequencer.GetRangeAsync(queryCursor, null, null, PAGE_SIZE);
            totalPageCount++;
            _logger.LogDebug("Backfill page {Page}: got {Count} events", totalPageCount, evts.Length);
            foreach (var t in evts)
            {
                totalEventCount++;
                yield return t;
            }

            var current = await _sequencer.CurrentAsync();
            var seqCursor = current ?? -1;
            _logger.LogDebug("Backfill page {Page}: seqCursor={SeqCursor}, LastSeen={LastSeen}, remaining={Remaining}", totalPageCount, seqCursor, LastSeen, seqCursor - LastSeen);
            if (seqCursor - LastSeen < PAGE_SIZE / 2)
            {
                _logger.LogInformation("Backfill complete after {Pages} pages, {Events} total events (seqCursor={SeqCursor}, LastSeen={LastSeen})", totalPageCount, totalEventCount, seqCursor, LastSeen);
                break;
            }
            if (evts.Length < 1)
            {
                _logger.LogInformation("Backfill got empty page after {Pages} pages, {Events} total events", totalPageCount, totalEventCount);
                break;
            }
        }
    }

    private void OnEvents(object? sender, ISeqEvt[] e)
    {
        _logger.LogDebug("OnEvents received {Count} events (caughtUp={CaughtUp})", e.Length, _caughtUp);
        if (_caughtUp)
        {
            foreach (var evt in e)
            {
                _logger.LogTrace("Writing event seq {Seq} to OutBuffer", evt.Seq);
                TryWriteOutput(evt);
            }
        }
        else
        {
            lock (_cutoverLock)
            {
                if (_caughtUp)
                {
                    _logger.LogDebug("Cutover already complete, writing {Count} events directly to OutBuffer", e.Length);
                    foreach (var evt in e)
                    {
                        _logger.LogTrace("Writing event seq {Seq} to OutBuffer", evt.Seq);
                        TryWriteOutput(evt);
                    }
                    return;
                }
                _logger.LogDebug("Cutover still in progress, buffering {Count} events", e.Length);
                foreach (var evt in e)
                {
                    _logger.LogTrace("Buffering event seq {Seq} to CutoverBuffer", evt.Seq);
                    CutoverBuffer.Enqueue(evt);
                }
            }
        }
    }


    void TryWriteOutput(ISeqEvt evt)
    {
        if (!OutBuffer.Writer.TryWrite(evt))
        {
            _logger.LogWarning("OutBuffer full (capacity={Capacity}), completing channel with ConsumerTooSlow", _opts.MaxBufferSize);
            OutBuffer.Writer.TryComplete(new XRPCError(new ErrorDetail("ConsumerTooSlow", "Stream consumer too slow")));
        }
    }
}