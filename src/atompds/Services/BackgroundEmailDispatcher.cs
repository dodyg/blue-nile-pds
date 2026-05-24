using Mailer;

namespace atompds.Services;

public class BackgroundEmailDispatcher
{
    private readonly IMailer _mailer;
    private readonly IBackgroundJobQueue _queue;

    public BackgroundEmailDispatcher(IMailer mailer, IBackgroundJobQueue queue)
    {
        _mailer = mailer;
        _queue = queue;
    }

    public Task SendCustomEmailAsync(string subject, string content, string to)
        => QueueAsync(mailer => mailer.SendCustomEmailAsync(subject, content, to));

    public Task SendAccountDeleteAsync(string token, string to)
        => QueueAsync(mailer => mailer.SendAccountDeleteAsync(token, to));

    public Task SendEmailConfirmationAsync(string token, string to)
        => QueueAsync(mailer => mailer.SendEmailConfirmationAsync(token, to));

    public Task SendEmailUpdateAsync(string token, string to)
        => QueueAsync(mailer => mailer.SendEmailUpdateAsync(token, to));

    public Task SendPasswordResetAsync(string token, string to)
        => QueueAsync(mailer => mailer.SendPasswordResetAsync(token, to));

    public Task SendPlcOperationSignatureAsync(string token, string to)
        => QueueAsync(mailer => mailer.SendPlcOperationSignatureAsync(token, to));

    private Task QueueAsync(Func<IMailer, Task> job)
    {
        var mailer = _mailer;
        return _queue.EnqueueAsync(_ => job(mailer)).AsTask();
    }
}
