using System.Net;
using DnsClient;

namespace Identity;

public class HandleResolver
{
    private readonly HttpClient _httpClient;
    public const string SUBDOMAIN = "_atproto";
    public const string PREFIX = "did=";
    
    public HandleResolver(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    public async Task<string?> Resolve(string handle, CancellationToken cancellationToken)
    {
        var dnsResult = await ResolveDns(handle);
        if (dnsResult != null)
        {
            return dnsResult;
        }

        var httpResult = await ResolveHttp(handle, cancellationToken);
        if (httpResult != null)
        {
            return httpResult;
        }

        return null;
    }
    
    public async Task<string?> ResolveDns(string handle)
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
        catch (Exception)
        {
            return null;
        }
    }
    
    public async Task<string?> ResolveHttp(string handle, CancellationToken cancellationToken)
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
        catch (Exception)
        {
            return null;
        }
    }
}