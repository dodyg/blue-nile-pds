using System.Text.Json.Serialization;

namespace BlueNilePds.Host.Endpoints.Admin.Models;

public record RepoResyncRequest
{
    [JsonPropertyName("did")]
    public string Did { get; init; } = "";
}

public record RepoResyncCreateOutput
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "";
    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; init; }
}

public record RepoResyncStatusOutput
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "";
    [JsonPropertyName("did")]
    public string? Did { get; init; }
    [JsonPropertyName("startedAt")]
    public DateTime? StartedAt { get; init; }
    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; init; }
    [JsonPropertyName("recordsScanned")]
    public int RecordsScanned { get; init; }
    [JsonPropertyName("recordsRewritten")]
    public int RecordsRewritten { get; init; }
    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
