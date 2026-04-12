using System.Threading.Channels;
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
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundJobWorker> _logger;

    public BackgroundJobWorker(
        BackgroundJobQueue queue,
        IServiceProvider serviceProvider,
        ILogger<BackgroundJobWorker> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background job worker started");
        await foreach (var job in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await job(_serviceProvider);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Background job failed");
            }
        }

        _logger.LogInformation("Background job worker stopped");
    }
}
