using System.Text.Json.Serialization;

namespace atompds.Endpoints.Admin.Models;

public record BackupCreateOutput
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "";
    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; init; }
}

public record BackupStatusOutput
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "";
    [JsonPropertyName("startedAt")]
    public DateTime? StartedAt { get; init; }
    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; init; }
    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }
    [JsonPropertyName("sizeBytes")]
    public long? SizeBytes { get; init; }
    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

public record BackupListOutput
{
    [JsonPropertyName("backups")]
    public List<BackupEntryOutput> Backups { get; init; } = [];
}

public record BackupEntryOutput
{
    [JsonPropertyName("fileName")]
    public string FileName { get; init; } = "";
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }
    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; init; }
}

public record BackupDeleteRequest
{
    [JsonPropertyName("fileName")]
    public string FileName { get; init; } = "";
}
