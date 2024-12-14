using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using Sequencer.Types;
using Xrpc;

namespace Sequencer;

public record OutboxOpts(int MaxBufferSize);

public class Outbox
{
    private readonly OutboxOpts _opts;
    private readonly SequencerRepository _sequencer;
    private bool _caughtUp;

    public Outbox(SequencerRepository sequencer, OutboxOpts opts)
    {
        _sequencer = sequencer;
        _opts = opts;
    }
    public int LastSeen { get; private set; } = -1;
    public Queue<ISeqEvt> OutBuffer { get; } = new();
    public Queue<ISeqEvt> CutoverBuffer { get; } = new();

    public async IAsyncEnumerable<ISeqEvt> Events(int? backfillCursor, [EnumeratorCancellation] CancellationToken token, WebSocket webSocket)
    {
        var returned = new List<ISeqEvt>();
        if (backfillCursor != null)
        {
            await foreach (var evt in GetBackfill(backfillCursor.Value, token))
            {
                if (token.IsCancellationRequested)
                {
                    yield break;
                }
                LastSeen = evt.Seq;
                returned.Add(evt);
                yield return evt;
            }
        }
        else
        {
            _caughtUp = true;
        }

        _sequencer.OnEvents += OnEvents;
        _sequencer.OnClose += (sender, args) => _sequencer.OnEvents -= OnEvents;

        await Cutover(backfillCursor);

        while (true)
        {
            if (token.IsCancellationRequested)
            {
                yield break;
            }
            if (webSocket.State != WebSocketState.Open)
            {
                yield break;
            }
            while (OutBuffer.TryDequeue(out var evt))
            {
                if (token.IsCancellationRequested)
                {
                    yield break;
                }
                if (evt.Seq > LastSeen)
                {
                    LastSeen = evt.Seq;
                    returned.Add(evt);
                    yield return evt;
                }

                if (OutBuffer.Count > _opts.MaxBufferSize)
                {
                    throw new XRPCError(new ErrorDetail("ConsumerTooSlow", "Stream consumer too slow"));
                }
            }
            await Task.Delay(100);
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
                OutBuffer.Enqueue(evt);
            }
            // dont worry about dupes, we ensure order on yield
            foreach (var evt in CutoverBuffer)
            {
                OutBuffer.Enqueue(evt);
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
        if (_caughtUp)
        {
            foreach (var evt in e)
            {
                OutBuffer.Enqueue(evt);
            }
        }
        else
        {
            foreach (var evt in e)
            {
                CutoverBuffer.Enqueue(evt);
            }
        }
    }
}