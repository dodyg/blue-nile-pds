using System.Net;
using System.Web;

namespace Identity;

public class DidWebResolver : BaseResolver
{
    public const string DOC_PATH = "/.well-known/did.json";
    private readonly HttpClient _client;
    
    private readonly TimeSpan _timeout;
    public DidWebResolver(TimeSpan timeout, IDidCache? didCache, HttpClient client) : base(didCache)
    {
        _timeout = timeout;
        _client = client;
    }
    
    // TODO: Handle when value is not found vs errors
    public override async Task<string?> ResolveNoCheck(string did)
    {
        var parsedId = string.Join(":", did.Split(':')[2..]);
        var parts = parsedId.Split(':').Select(HttpUtility.UrlDecode).ToArray();
        string path;
        if (parsedId.Length < 1)
        {
            throw new PoorlyFormattedDidError(did);
        } 
        else if (parts.Length == 1)
        {
            path = parts[0] + DOC_PATH;
        }
        else
        {
            path = parts[1] ?? throw new PoorlyFormattedDidError(did);
        }
        
        var url = new Uri(path);
        if (url.Host == "localhost")
        {
            // set scheme to http
            url = new UriBuilder(url) { Scheme = "http" }.Uri;
        }

        try
        {
            using var cts = new CancellationTokenSource(_timeout);
            var res = await _client.GetAsync(url, cts.Token);
            res.EnsureSuccessStatusCode();
            var content = await res.Content.ReadAsStringAsync(CancellationToken.None);
            return content;
        }
        // 404
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}