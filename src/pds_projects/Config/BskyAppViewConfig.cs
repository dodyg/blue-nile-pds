namespace Config;

public interface IBskyAppViewConfig;
public record DisabledBskyAppViewConfig : IBskyAppViewConfig;

public record BskyAppViewConfig : IBskyAppViewConfig
{
    public required string Url { get; init; }
    public required string Did { get; init; }
    public string? CdnUrlPattern { get; init; }
}