namespace Mailer;

public interface IMailer
{
    public Task SendAccountDeleteAsync(string token, string to);
}