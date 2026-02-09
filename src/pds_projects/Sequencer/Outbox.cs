using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Sequencer.Types;
using Xrpc;

namespace Sequencer;

public record OutboxOpts(int MaxBufferSize);

public class Outbox
{
    private readonly ILogger<Outbox> _logger;
    private readonly OutboxOpts _opts;
    private readonly SequencerRepository _sequencer;
    private bool _caughtUp;

    public Outbox(SequencerRepository sequencer, OutboxOpts opts, ILogger<Outbox> logger)
    {
        _sequencer = sequencer;
        _opts = opts;
        _logger = logger;
        OutBuffer = Channel.CreateBounded<ISeqEvt>(opts.MaxBufferSize);
    }
    public int LastSeen { get; private set; } = -1;
    public Channel<ISeqEvt> OutBuffer { get; private init; }
    public ConcurrentQueue<ISeqEvt> CutoverBuffer { get; } = new();

    public async IAsyncEnumerable<ISeqEvt> Events(int? backfillCursor, [EnumeratorCancellation] CancellationToken token, WebSocket webSocket)
    {
        if (backfillCursor != null)
        {
            await foreach (var evt in GetBackfill(backfillCursor.Value, token))
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

        _sequencer.OnEvents += OnEvents;
        _sequencer.OnClose += (sender, args) => _sequencer.OnEvents -= OnEvents;

        try
        {
            await Cutover(backfillCursor);

            // there is a potential problem here as the channel will only throw the too slow exception only when the consumer consumes to the end 
            // it will now throw when it gets full immediately
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
            _sequencer.OnEvents -= OnEvents;
        }
    }

    private async Task Cutover(int? backfillCursor)
    {
        // only need to perform cutover if we've been backfilling
        if (backfillCursor != null)
        {
            var cutoverEvts = await _sequencer.GetRange(LastSeen > -1 ? LastSeen : backfillCursor.Value, null, null, null);
            foreach (var evt in cutoverEvts)
            {
                TryWriteOutput(evt);
            }
            // dont worry about dupes, we ensure order on yield
            foreach (var evt in CutoverBuffer)
            {
                TryWriteOutput(evt);
            }
            _caughtUp = true;
            CutoverBuffer.Clear();
        }
        else
        {
            _caughtUp = true;
        }
    }

    public async IAsyncEnumerable<ISeqEvt> GetBackfill(int backfillCursor, [EnumeratorCancellation] CancellationToken token)
    {
        const int PAGE_SIZE = 500;
        while (true)
        {
            if (token.IsCancellationRequested)
            {
                yield break;
            }

            var evts = await _sequencer.GetRange(LastSeen > -1 ? LastSeen : backfillCursor, null, null, PAGE_SIZE);
            foreach (var t in evts)
            {
                yield return t;
            }

            var seqCursor = _sequencer.LastSeen ?? -1;
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
        // there is probalby still a race condition here as _caughtUp could be set to true after the if check but before the events are written to the cutover buffer
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
            foreach (var evt in e)
            {
                _logger.LogDebug("Buffering event with seq {Seq} to CutoverBuffer", evt.Seq);
                CutoverBuffer.Enqueue(evt);
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