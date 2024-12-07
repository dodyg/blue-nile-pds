using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Sequencer;

public record CrawlersConfig(string Hostname, string[] Crawlers);
public class Crawlers
{
    public DateTime LastNotified { get; private set; }
    public static readonly TimeSpan NotifyThreshold = TimeSpan.FromMinutes(20);
    
    private readonly CrawlersConfig _config;
    private readonly HttpClient _client;
    private readonly ILogger<Crawlers> _logger;


    public Crawlers(CrawlersConfig config, HttpClient client, ILogger<Crawlers> logger)
    {
        LastNotified = DateTime.MinValue;
        _config = config;
        _client = client;
        _logger = logger;
    }

    public async Task NotifyOfUpdate()
    {
        if (DateTime.UtcNow - LastNotified < NotifyThreshold)
        {
            return;
        }
        
        var crawlTasks = new List<Task>();
        foreach (var host in _config.Crawlers)
        {
            crawlTasks.Add(RequestCrawl(host));
        }
        
        await Task.WhenAll(crawlTasks);
        LastNotified = DateTime.UtcNow;
    }

    private async Task RequestCrawl(string host)
    {
        var lh = host.Trim();
        _logger.LogInformation("Requesting crawl from {Host}", lh);
        if (!lh.StartsWith("http://") && !lh.StartsWith("https://"))
        {
            lh = $"https://{host}";
        }
            
        var response = await _client.PostAsJsonAsync($"{lh}/xrpc/com.atproto.sync.requestCrawl", new
        {
            hostname = _config.Hostname
        });
            
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to request crawl from {Host}", lh);
        }
    }
}