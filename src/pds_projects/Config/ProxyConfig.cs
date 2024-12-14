namespace Config;

public record ProxyConfig
{
    public bool DisableSsrfProtection { get; init; }
    public bool AllowHTTP2 { get; init; }
    public int HeadersTimeout { get; init; }
    public int BodyTimeout { get; init; }
    public long MaxResponseSize { get; init; }
    public int MaxRetries { get; init; }
    public bool PreferCompressed { get; init; }
}