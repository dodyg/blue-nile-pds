using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Mailer;

public record SmtpMailerConfig(string Host, int Port, string? Username, string? Password, string FromAddress, bool UseTls);

public class SmtpMailer : IMailer
{
    private readonly ILogger<SmtpMailer> _logger;
    private readonly SmtpMailerConfig _config;

    public SmtpMailer(SmtpMailerConfig config, ILogger<SmtpMailer> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAccountDeleteAsync(string token, string to)
    {
        var subject = "Account Deletion";
        var body = $"Your account deletion token is: {token}";
        await SendEmailAsync(to, subject, body);
    }

    public async Task SendEmailConfirmationAsync(string token, string to)
    {
        var subject = "Confirm Your Email";
        var body = $"Your email confirmation code is: {token}";
        await SendEmailAsync(to, subject, body);
    }

    public async Task SendEmailUpdateAsync(string token, string to)
    {
        var subject = "Confirm Email Update";
        var body = $"Your email update confirmation code is: {token}";
        await SendEmailAsync(to, subject, body);
    }

    public async Task SendPasswordResetAsync(string token, string to)
    {
        var subject = "Password Reset";
        var body = $"Your password reset code is: {token}";
        await SendEmailAsync(to, subject, body);
    }

    public async Task SendPlcOperationSignatureAsync(string token, string to)
    {
        var subject = "PLC Operation Verification";
        var body = $"Your PLC operation verification code is: {token}";
        await SendEmailAsync(to, subject, body);
    }

    private async Task SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            using var message = new MimeMessage();
            message.From.Add(new MailboxAddress("PDS", _config.FromAddress));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();
            await client.ConnectAsync(_config.Host, _config.Port, _config.UseTls);

            if (!string.IsNullOrWhiteSpace(_config.Username) && !string.IsNullOrWhiteSpace(_config.Password))
            {
                await client.AuthenticateAsync(_config.Username, _config.Password);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Sent email to {to} with subject '{subject}'", to, subject);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to send email to {to}", to);
            throw;
        }
    }
}
