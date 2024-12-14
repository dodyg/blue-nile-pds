namespace Identity;

public class IdResolver
{

    public IdResolver(IdentityResolverOpts opts, HttpClient httpClient)
    {
        HandleResolver = new HandleResolver(httpClient);
        DidResolver = new DidResolver(TimeSpan.FromMilliseconds(opts.TimeoutMs), opts.PlcUrl, opts.DidCache, httpClient);
    }
    public HandleResolver HandleResolver { get; }
    public DidResolver DidResolver { get; }
}