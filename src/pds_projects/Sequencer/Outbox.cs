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
        if (backfillCursor != null)
        {
            await foreach (var evt in GetBackfillAsync(backfillCursor.Value, token))
            {
                if (token.IsCancellationRequested)
                {
                    yield break;
                }
                LastSeen = evt.Seq;
                yield return evt;
            }
        }
        else
        {
            _caughtUp = true;
        }

        _eventSource.OnEvents += OnEvents;

        try
        {
            await CutoverAsync(backfillCursor);

            await foreach (var evt in OutBuffer.Reader.ReadAllAsync(token))
            {
                if (webSocket.State != WebSocketState.Open)
                {
                    yield break;
                }
                if (evt.Seq > LastSeen)
                {
                    _logger.LogDebug("Yielding event with seq {Seq}", evt.Seq);
                    LastSeen = evt.Seq;
                    yield return evt;
                }
            }
        }
        finally
        {
            _eventSource.OnEvents -= OnEvents;
        }
    }

    private async Task CutoverAsync(int? backfillCursor)
    {
        if (backfillCursor != null)
        {
            var cutoverEvts = await _sequencer.GetRangeAsync(LastSeen > -1 ? LastSeen : backfillCursor.Value, null, null, null);
            foreach (var evt in cutoverEvts)
            {
                TryWriteOutput(evt);
            }
            lock (_cutoverLock)
            {
                foreach (var evt in CutoverBuffer)
                {
                    TryWriteOutput(evt);
                }
                _caughtUp = true;
                CutoverBuffer.Clear();
            }
        }
        else
        {
            _caughtUp = true;
        }
    }

    public async IAsyncEnumerable<ISeqEvt> GetBackfillAsync(int backfillCursor, [EnumeratorCancellation] CancellationToken token)
    {
        const int PAGE_SIZE = 500;
        while (true)
        {
            if (token.IsCancellationRequested)
            {
                yield break;
            }

            var evts = await _sequencer.GetRangeAsync(LastSeen > -1 ? LastSeen : backfillCursor, null, null, PAGE_SIZE);
            foreach (var t in evts)
            {
                yield return t;
            }

            var current = await _sequencer.CurrentAsync();
            var seqCursor = current ?? -1;
            if (seqCursor - LastSeen < PAGE_SIZE / 2)
            {
                break;
            }
            if (evts.Length < 1)
            {
                break;
            }
        }
    }

    private void OnEvents(object? sender, ISeqEvt[] e)
    {
        if (_caughtUp)
        {
            foreach (var evt in e)
            {
                _logger.LogDebug("Trying to write event with seq {Seq} to OutBuffer", evt.Seq);
                TryWriteOutput(evt);
            }
        }
        else
        {
            lock (_cutoverLock)
            {
                if (_caughtUp)
                {
                    foreach (var evt in e)
                    {
                        _logger.LogDebug("Trying to write event with seq {Seq} to OutBuffer", evt.Seq);
                        TryWriteOutput(evt);
                    }
                    return;
                }
                foreach (var evt in e)
                {
                    _logger.LogDebug("Buffering event with seq {Seq} to CutoverBuffer", evt.Seq);
                    CutoverBuffer.Enqueue(evt);
                }
            }
        }
    }


    void TryWriteOutput(ISeqEvt evt)
    {
        if (!OutBuffer.Writer.TryWrite(evt))
        {
            OutBuffer.Writer.TryComplete(new XRPCError(new ErrorDetail("ConsumerTooSlow", "Stream consumer too slow")));
        }
    }
}