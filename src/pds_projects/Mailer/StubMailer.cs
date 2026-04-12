using Microsoft.Extensions.Logging;

namespace Mailer;

public class StubMailer : IMailer
{
    private readonly ILogger<StubMailer> _logger;
    public StubMailer(ILogger<StubMailer> logger)
    {
        _logger = logger;
    }

    public Task SendAccountDeleteAsync(string token, string to)
    {
        _logger.LogInformation("[STUB] Sending account delete email to {to} with token {token}", to, token);
        return Task.CompletedTask;
    }

    public Task SendEmailConfirmationAsync(string token, string to)
    {
        _logger.LogInformation("[STUB] Sending email confirmation to {to} with token {token}", to, token);
        return Task.CompletedTask;
    }

    public Task SendEmailUpdateAsync(string token, string to)
    {
        _logger.LogInformation("[STUB] Sending email update to {to} with token {token}", to, token);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string token, string to)
    {
        _logger.LogInformation("[STUB] Sending password reset to {to} with token {token}", to, token);
        return Task.CompletedTask;
    }

    public Task SendPlcOperationSignatureAsync(string token, string to)
    {
        _logger.LogInformation("[STUB] Sending PLC operation signature to {to} with token {token}", to, token);
        return Task.CompletedTask;
    }
}
