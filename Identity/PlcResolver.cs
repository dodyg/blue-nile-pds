using System.Net;
using System.Web;

namespace Identity;

public class PlcResolver : BaseResolver
{
    private readonly HttpClient _client;
    private readonly TimeSpan _timeout;
    private readonly string _plcUrl;
    public PlcResolver(TimeSpan timeout, string plcUrl, IDidCache didCache, HttpClient client) : base(didCache)
    {
        _client = client;
        _timeout = timeout;
        _plcUrl = plcUrl;
    }

    public override async Task<string?> ResolveNoCheck(string did)
    {
        var encodedDid = HttpUtility.UrlPathEncode(did);
        try
        {
            using var cts = new CancellationTokenSource(_timeout);
            var response = await _client.GetAsync($"{_plcUrl}/{encodedDid}", cts.Token);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync(CancellationToken.None);
            return content;
        }
        // 404
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}