using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace BlueNilePds.Pds.Mailer;

public record SmtpMailerConfig(string Host, int Port, string? Username, string? Password, string FromAddress);

public class SmtpMailer : IMailer
{
    private readonly ILogger<SmtpMailer> _logger;
    private readonly SmtpMailerConfig _config;
    private readonly string _serviceName;

    public SmtpMailer(SmtpMailerConfig config, string serviceName, ILogger<SmtpMailer> logger)
    {
        _config = config;
        _serviceName = serviceName;
        _logger = logger;
    }

    public Task SendAccountDeleteAsync(string token, string to, string? handle, DateTimeOffset issuedAt) =>
        SendCodeAsync(EmailPurpose.AccountDelete, token, to, handle, issuedAt);

    public Task SendEmailConfirmationAsync(string token, string to, string? handle, DateTimeOffset issuedAt) =>
        SendCodeAsync(EmailPurpose.ConfirmEmail, token, to, handle, issuedAt);

    public Task SendEmailUpdateAsync(string token, string to, string? handle, DateTimeOffset issuedAt) =>
        SendCodeAsync(EmailPurpose.EmailUpdate, token, to, handle, issuedAt);

    public Task SendPasswordResetAsync(string token, string to, string? handle, DateTimeOffset issuedAt) =>
        SendCodeAsync(EmailPurpose.PasswordReset, token, to, handle, issuedAt);

    public Task SendPlcOperationSignatureAsync(string token, string to, string? handle, DateTimeOffset issuedAt) =>
        SendCodeAsync(EmailPurpose.PlcOperation, token, to, handle, issuedAt);

    public Task SendCustomEmailAsync(string subject, string content, string to, string? handle) =>
        SendEmailAsync(to, EmailTemplates.RenderCustom(subject, content, handle, _serviceName), "custom");

    public static MimeMessage BuildMessage(RenderedEmail email, string to, string fromName, string fromAddress)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = email.Subject;
        message.Body = new BodyBuilder { TextBody = email.Text, HtmlBody = email.Html }.ToMessageBody();
        return message;
    }

    private Task SendCodeAsync(EmailPurpose purpose, string token, string to, string? handle, DateTimeOffset issuedAt) =>
        SendEmailAsync(to, EmailTemplates.Render(purpose, token, handle, _serviceName, issuedAt), purpose.ToString());

    // The subject carries the code, so only the purpose is logged.
    private async Task SendEmailAsync(string to, RenderedEmail email, string purpose)
    {
        try
        {
            using var message = BuildMessage(email, to, _serviceName, _config.FromAddress);

            using var client = new SmtpClient();
            await client.ConnectAsync(_config.Host, _config.Port, SecureSocketOptions.StartTls);

            if (!string.IsNullOrWhiteSpace(_config.Username) && !string.IsNullOrWhiteSpace(_config.Password))
            {
                await client.AuthenticateAsync(_config.Username, _config.Password);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Sent {purpose} email to {to}", purpose, to);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to send {purpose} email to {to}", purpose, to);
            throw;
        }
    }
}
