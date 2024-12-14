namespace Identity;

public class DidResolver : BaseResolver
{
    private readonly Dictionary<string, BaseResolver> _methods;
    public DidResolver(TimeSpan timeout, string plcUrl, IDidCache didCache, HttpClient httpClient) : base(didCache)
    {
        _methods = new Dictionary<string, BaseResolver>
        {
            {"plc", new PlcResolver(timeout, plcUrl, didCache, httpClient)},
            {"web", new DidWebResolver(timeout, didCache, httpClient)}
        };
    }

    public override Task<string?> ResolveNoCheck(string did)
    {
        var split = did.Split(':');
        if (split[0] != "did")
        {
            throw new PoorlyFormattedDidError(did);
        }

        var method = split[1];
        if (!_methods.TryGetValue(method, out var resolver))
        {
            throw new UnsupportedDidMethodError(did);
        }

        return resolver.ResolveNoCheck(did);
    }
}

public class PoorlyFormattedDidError(string did) : Exception($"Poorly formatted DID: {did}");
public class UnsupportedDidMethodError(string did) : Exception($"Unsupported DID method: {did}");