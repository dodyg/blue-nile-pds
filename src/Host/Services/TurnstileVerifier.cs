using System.Text.Json;
using System.Text.Json.Serialization;
using BlueNilePds.Pds.Xrpc;

namespace BlueNilePds.Host.Services;

public class TurnstileVerifier
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TurnstileVerifier> _logger;
    private readonly string? _secret;

    public TurnstileVerifier(HttpClient httpClient, ILogger<TurnstileVerifier> logger, string? secret)
    {
        _httpClient = httpClient;
        _logger = logger;
        _secret = secret;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_secret);

    public async Task VerifyAsync(string? token)
    {
        if (!IsConfigured)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Turnstile verification is required"));
        }

        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = _secret!,
                ["response"] = token
            });

            var response = await _httpClient.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify", content);
            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<TurnstileResponse>(responseBody);

            if (result == null || !result.Success)
            {
                _logger.LogWarning("Turnstile verification failed: {Response}", responseBody);
                throw new XRPCError(new InvalidRequestErrorDetail("Turnstile verification failed"));
            }
        }
        catch (XRPCError)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to verify Turnstile token");
            throw new XRPCError(new InvalidRequestErrorDetail("Turnstile verification failed"));
        }
    }

    private record TurnstileResponse([property: JsonPropertyName("success")] bool Success);
}
