using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace atompds.Services;

public interface IBackgroundJobQueue
{
    ValueTask EnqueueAsync(Func<IServiceProvider, Task> job, CancellationToken ct = default);
}

public class BackgroundJobQueue : IBackgroundJobQueue
{
    private const int Capacity = 1000;
    private readonly Channel<Func<IServiceProvider, Task>> _channel = Channel.CreateBounded<Func<IServiceProvider, Task>>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    private readonly ILogger<BackgroundJobQueue> _logger;
    private int _pendingCount;

    public BackgroundJobQueue(ILogger<BackgroundJobQueue> logger)
    {
        _logger = logger;
    }

    public ChannelWriter<Func<IServiceProvider, Task>> Writer => _channel.Writer;

    public ValueTask EnqueueAsync(Func<IServiceProvider, Task> job, CancellationToken ct = default)
    {
        var pending = Interlocked.Increment(ref _pendingCount);
        if (pending >= Capacity)
        {
            Interlocked.Exchange(ref _pendingCount, Capacity);
            _logger.LogWarning("Background job queue reached capacity {Capacity}; the oldest queued work item may be dropped", Capacity);
        }

        return _channel.Writer.WriteAsync(job, ct);
    }

    public async IAsyncEnumerable<Func<IServiceProvider, Task>> DequeueAllAsync([EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var job in _channel.Reader.ReadAllAsync(ct))
        {
            Interlocked.Decrement(ref _pendingCount);
            yield return job;
        }
    }
}

public class BackgroundJobWorker : BackgroundService
{
    private readonly BackgroundJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundJobWorker> _logger;

    public BackgroundJobWorker(
        BackgroundJobQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundJobWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background job worker started");
        await foreach (var job in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await job(scope.ServiceProvider);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Background job failed");
            }
        }

        _logger.LogInformation("Background job worker stopped");
    }
}
