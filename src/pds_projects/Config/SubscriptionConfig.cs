namespace Config;

public record SubscriptionConfig
{
    public required int MaxSubscriptionBuffer { get; init; }
    public required int RepoBackfillLimitMs { get; init; }
}