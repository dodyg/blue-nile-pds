using System.Text.Json.Serialization;

namespace CommonWeb;

public class DidDocument
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    [JsonPropertyName("alsoKnownAs")]
    public required string[]? AlsoKnownAs { get; init; }
    [JsonPropertyName("verificationMethod")]
    public required VerificationMethod[]? VerificationMethod { get; init; }
    [JsonPropertyName("service")]
    public required Service[]? Service { get; init; }
}

public class VerificationMethod
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    [JsonPropertyName("type")]
    public required string Type { get; init; }
    [JsonPropertyName("controller")]
    public required string Controller { get; init; }
    [JsonPropertyName("publicKeyMultibase")]
    public required string? PublicKeyMultibase { get; init; }
}

public class Service
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    [JsonPropertyName("type")]
    public required string Type { get; init; }
    [JsonPropertyName("serviceEndpoint")]
    public required string ServiceEndpoint { get; init; } // TODO: union, string/object, might be better to define this field as jsonelement
}