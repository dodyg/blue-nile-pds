using Microsoft.Extensions.Logging;

namespace Mailer;

public class StubMailer : IMailer
{
    private readonly ILogger<StubMailer> _logger;
    public StubMailer(ILogger<StubMailer> logger)
    {
        _logger = logger;
    }

    public Task SendAccountDelete(string token, string to)
    {
        _logger.LogInformation("Sending account delete email to {to} with token {token}", to, token);
        return Task.CompletedTask;
    }
}