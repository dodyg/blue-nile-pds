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
    private readonly Channel<Func<IServiceProvider, Task>> _channel = Channel.CreateBounded<Func<IServiceProvider, Task>>(
        new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public ChannelWriter<Func<IServiceProvider, Task>> Writer => _channel.Writer;

    public ValueTask EnqueueAsync(Func<IServiceProvider, Task> job, CancellationToken ct = default)
    {
        return _channel.Writer.WriteAsync(job, ct);
    }

    public IAsyncEnumerable<Func<IServiceProvider, Task>> DequeueAllAsync(CancellationToken ct)
    {
        return _channel.Reader.ReadAllAsync(ct);
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
