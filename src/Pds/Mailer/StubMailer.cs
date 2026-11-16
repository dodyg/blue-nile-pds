using Microsoft.Extensions.Logging;

namespace BlueNilePds.Pds.Mailer;

public class StubMailer : IMailer
{
    private readonly ILogger<StubMailer> _logger;
    public StubMailer(ILogger<StubMailer> logger)
    {
        _logger = logger;
    }

    public Task SendCustomEmailAsync(string subject, string content, string to, string? handle)
    {
        _logger.LogInformation("[STUB] Sending custom email to {to} about {handle} with subject '{subject}' and content '{content}'", to, handle, subject, content);
        return Task.CompletedTask;
    }

    public Task SendAccountDeleteAsync(string token, string to, string? handle, DateTimeOffset issuedAt)
    {
        _logger.LogInformation("[STUB] Sending account delete email to {to} about {handle} with token [REDACTED]", to, handle);
        return Task.CompletedTask;
    }

    public Task SendEmailConfirmationAsync(string token, string to, string? handle, DateTimeOffset issuedAt)
    {
        _logger.LogInformation("[STUB] Sending email confirmation to {to} about {handle} with token [REDACTED]", to, handle);
        return Task.CompletedTask;
    }

    public Task SendEmailUpdateAsync(string token, string to, string? handle, DateTimeOffset issuedAt)
    {
        _logger.LogInformation("[STUB] Sending email update to {to} about {handle} with token [REDACTED]", to, handle);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string token, string to, string? handle, DateTimeOffset issuedAt)
    {
        _logger.LogInformation("[STUB] Sending password reset to {to} about {handle} with token [REDACTED]", to, handle);
        return Task.CompletedTask;
    }

    public Task SendPlcOperationSignatureAsync(string token, string to, string? handle, DateTimeOffset issuedAt)
    {
        _logger.LogInformation("[STUB] Sending PLC operation signature to {to} about {handle} with token [REDACTED]", to, handle);
        return Task.CompletedTask;
    }
}
