namespace Mailer;

public interface IMailer
{
    public Task SendAccountDeleteAsync(string token, string to);
    public Task SendEmailConfirmationAsync(string token, string to);
    public Task SendEmailUpdateAsync(string token, string to);
    public Task SendPasswordResetAsync(string token, string to);
    public Task SendPlcOperationSignatureAsync(string token, string to);
}
