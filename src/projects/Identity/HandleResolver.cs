using System.Net;
using DnsClient;
using Microsoft.Extensions.Logging;

namespace Identity;

public class HandleResolver
{
    public const string SUBDOMAIN = "_atproto";
    public const string PREFIX = "did=";
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public HandleResolver(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string?> ResolveAsync(string handle, CancellationToken cancellationToken)
    {
        var dnsResult = await ResolveDnsAsync(handle);
        if (dnsResult != null)
        {
            return dnsResult;
        }

        var httpResult = await ResolveHttpAsync(handle, cancellationToken);
        if (httpResult != null)
        {
            return httpResult;
        }

        return null;
    }

    public async Task<string?> ResolveDnsAsync(string handle)
    {
        try
        {
            var lookup = new LookupClient();
            var result = await lookup.QueryAsync($"{SUBDOMAIN}.{handle}", QueryType.TXT);
            var records = result.Answers.TxtRecords().ToArray();
            if (records.Length == 0)
            {
                return null;
            }

            foreach (var txtRecord in records)
            {
                var txt = string.Join("", txtRecord.Text);
                if (txt.StartsWith(PREFIX))
                {
                    return txt[PREFIX.Length..];
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "DNS resolution failed for handle {Handle}", handle);
            return null;
        }
    }

    public async Task<string?> ResolveHttpAsync(string handle, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync($"https://{handle}/.well-known/atproto-did", cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            var line = content.Split('\n').FirstOrDefault();
            if (line == null || !line.StartsWith(PREFIX))
            {
                return null;
            }

            return line;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "HTTP resolution failed for handle {Handle}", handle);
            return null;
        }
    }
}