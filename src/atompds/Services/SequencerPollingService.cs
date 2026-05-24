using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sequencer;
using Sequencer.Db;
using Sequencer.Types;
using System.Threading.Channels;

namespace atompds.Services;

public class SequencerPollingService : BackgroundService, Sequencer.ISequencerEventSource
{
    private readonly IDbContextFactory<SequencerDb> _seqDbFactory;
    private readonly ILogger<SequencerPollingService> _logger;

    public SequencerPollingService(
        IDbContextFactory<SequencerDb> seqDbFactory,
        ILogger<SequencerPollingService> logger)
    {
        _seqDbFactory = seqDbFactory;
        _logger = logger;
    }

    public int? LastSeen { get; private set; }
    private int TriesWithNoResults { get; set; }

    public event EventHandler<ISeqEvt[]>? OnEvents;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Starting sequencer poll service");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollDbAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error polling db");
            }
        }
    }

    private async Task PollDbAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var db = _seqDbFactory.CreateDbContext();
            _logger.LogDebug("Polling db for new events since {LastSeen}", LastSeen);

            var seqs = db.RepoSeqs.AsQueryable()
                .OrderBy(x => x.Seq)
                .Where(x => x.Invalidated == false);

            if (LastSeen != null)
            {
                seqs = seqs.Where(x => x.Seq > LastSeen);
            }

            var rows = await seqs.Take(1000).ToArrayAsync(cancellationToken);

            if (rows.Length > 0)
            {
                _logger.LogInformation("Found {Count} new events", rows.Length);
                TriesWithNoResults = 0;

                var seqEvents = new List<ISeqEvt>();
                foreach (var row in rows)
                {
                    try
                    {
                        var evt = SequencerRepository.DecodeSeqEvent(row);
                        if (evt != null) seqEvents.Add(evt);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error decoding event with seq {Seq}", row.Seq);
                    }
                }

                if (seqEvents.Count > 0)
                {
                    OnEvents?.Invoke(this, seqEvents.ToArray());
                }
                LastSeen = rows.Max(x => x.Seq);
            }
            else
            {
                _logger.LogDebug("No new events found");
                await ExponentialBackoffAsync(cancellationToken);
            }
        }
        catch (Exception)
        {
            await ExponentialBackoffAsync(cancellationToken);
        }
    }

    private async Task ExponentialBackoffAsync(CancellationToken cancellationToken)
    {
        TriesWithNoResults++;
        var delay = Math.Pow(2, TriesWithNoResults);
        var delayLength = Math.Min(1000, (int)delay);
        _logger.LogDebug("Waiting {DelayLength}ms before next poll", delayLength);
        await Task.Delay(delayLength, cancellationToken);
    }
}
