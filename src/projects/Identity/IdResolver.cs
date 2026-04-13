using Microsoft.Extensions.Logging;

namespace Identity;

public class IdResolver
{

    public IdResolver(IdentityResolverOpts opts, HttpClient httpClient, ILogger<IdResolver> logger)
    {
        HandleResolver = new HandleResolver(httpClient, logger);
        DidResolver = new DidResolver(TimeSpan.FromMilliseconds(opts.TimeoutMs), opts.PlcUrl, opts.DidCache, httpClient);
    }
    public HandleResolver HandleResolver { get; }
    public DidResolver DidResolver { get; }
}
