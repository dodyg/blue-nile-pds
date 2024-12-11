namespace Mailer;

public interface IMailer
{
    public Task SendAccountDelete(string token, string to);
}