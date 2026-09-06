using System.Text.Json.Serialization;

namespace BlueNilePds.Core.Did;

public record Service
{
    [JsonPropertyName("type")] public required string Type { get; init; }

    [JsonPropertyName("endpoint")] public required string Endpoint { get; init; }
}