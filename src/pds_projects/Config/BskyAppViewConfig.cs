namespace Config;

public interface IBskyAppViewConfig;
public record DisabledBskyAppViewConfig : IBskyAppViewConfig;

public record BskyAppViewConfig : IBskyAppViewConfig
{
    public string Url { get; init; }
    public string Did { get; init; }
    public string? CdnUrlPattern { get; init; }
}