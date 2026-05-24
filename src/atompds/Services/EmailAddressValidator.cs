using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Xrpc;

namespace atompds.Services;

public class EmailAddressValidator
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EmailAddressValidator> _logger;

    public EmailAddressValidator(HttpClient httpClient, ILogger<EmailAddressValidator> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task AssertSupportedEmailAsync(string email)
    {
        if (!IsValidEmail(email) || await IsDisposableEmailAsync(email))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("This email address is not supported, please use a different email."));
        }
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new MailAddress(email);
            return addr.Address == email;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private async Task<bool> IsDisposableEmailAsync(string email)
    {
        try
        {
            var response = await _httpClient.GetAsync($"https://open.kickbox.com/v1/disposable/{email}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Disposable email lookup failed with status {StatusCode}", response.StatusCode);
                return false;
            }

            var content = await response.Content.ReadAsStringAsync();
            var disposableResponse = JsonSerializer.Deserialize<DisposableResponse>(content);
            return disposableResponse?.Disposable == true;
        }
        catch (HttpRequestException e)
        {
            _logger.LogError(e, "Failed to check if email is disposable");
            return false;
        }
        catch (TaskCanceledException e)
        {
            _logger.LogError(e, "Timed out checking if email is disposable");
            return false;
        }
        catch (JsonException e)
        {
            _logger.LogError(e, "Failed to parse disposable email response");
            return false;
        }
    }

    private record DisposableResponse([property: JsonPropertyName("disposable")] bool Disposable);
}
