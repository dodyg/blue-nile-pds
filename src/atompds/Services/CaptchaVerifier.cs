using System.Text.Json;
using Microsoft.Extensions.Logging;
using Xrpc;

namespace atompds.Services;

public class CaptchaVerifier
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CaptchaVerifier> _logger;
    private readonly string? _secret;

    public CaptchaVerifier(HttpClient httpClient, ILogger<CaptchaVerifier> logger, string? secret)
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
            throw new XRPCError(new InvalidRequestErrorDetail("Captcha verification is required"));
        }

        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = _secret!,
                ["response"] = token
            });

            var response = await _httpClient.PostAsync("https://api.hcaptcha.com/siteverify", content);
            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<HcaptchaResponse>(responseBody);

            if (result == null || !result.Success)
            {
                _logger.LogWarning("hCaptcha verification failed: {Response}", responseBody);
                throw new XRPCError(new InvalidRequestErrorDetail("Captcha verification failed"));
            }
        }
        catch (XRPCError)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to verify hCaptcha");
            throw new XRPCError(new InvalidRequestErrorDetail("Captcha verification failed"));
        }
    }

    private record HcaptchaResponse(bool Success);
}
