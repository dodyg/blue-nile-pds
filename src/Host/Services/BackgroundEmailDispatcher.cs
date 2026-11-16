using BlueNilePds.Pds.Mailer;

namespace BlueNilePds.Host.Services;

public class BackgroundEmailDispatcher
{
    private readonly IMailer _mailer;
    private readonly IBackgroundJobQueue _queue;
    private readonly TimeProvider _time;

    // The queue has one worker that also runs backups, so an email can go out well after its code was made;
    // the time is taken here, when the code is queued, so the email's expiry matches the code's.
    public BackgroundEmailDispatcher(IMailer mailer, IBackgroundJobQueue queue, TimeProvider? time = null)
    {
        _mailer = mailer;
        _queue = queue;
        _time = time ?? TimeProvider.System;
    }

    public Task SendCustomEmailAsync(string subject, string content, string to, string? handle)
        => QueueAsync(mailer => mailer.SendCustomEmailAsync(subject, content, to, handle));

    public Task SendAccountDeleteAsync(string token, string to, string? handle)
    {
        var issuedAt = _time.GetUtcNow();
        return QueueAsync(mailer => mailer.SendAccountDeleteAsync(token, to, handle, issuedAt));
    }

    public Task SendEmailConfirmationAsync(string token, string to, string? handle)
    {
        var issuedAt = _time.GetUtcNow();
        return QueueAsync(mailer => mailer.SendEmailConfirmationAsync(token, to, handle, issuedAt));
    }

    public Task SendEmailUpdateAsync(string token, string to, string? handle)
    {
        var issuedAt = _time.GetUtcNow();
        return QueueAsync(mailer => mailer.SendEmailUpdateAsync(token, to, handle, issuedAt));
    }

    public Task SendPasswordResetAsync(string token, string to, string? handle)
    {
        var issuedAt = _time.GetUtcNow();
        return QueueAsync(mailer => mailer.SendPasswordResetAsync(token, to, handle, issuedAt));
    }

    public Task SendPlcOperationSignatureAsync(string token, string to, string? handle)
    {
        var issuedAt = _time.GetUtcNow();
        return QueueAsync(mailer => mailer.SendPlcOperationSignatureAsync(token, to, handle, issuedAt));
    }

    private Task QueueAsync(Func<IMailer, Task> job)
    {
        var mailer = _mailer;
        return _queue.EnqueueAsync(_ => job(mailer)).AsTask();
    }
}
