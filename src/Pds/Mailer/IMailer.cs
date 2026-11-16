namespace BlueNilePds.Pds.Mailer;

/// <summary>
/// Sends the account emails. Every method takes the handle of the account the email acts on, so the email can name it
/// (several accounts may share one inbox). The handle is null only when the account has none yet.
/// Code emails also take the time the code was made, since the email may go out later and its expiry counts from then.
/// </summary>
public interface IMailer
{
    public Task SendCustomEmailAsync(string subject, string content, string to, string? handle);
    public Task SendAccountDeleteAsync(string token, string to, string? handle, DateTimeOffset issuedAt);
    public Task SendEmailConfirmationAsync(string token, string to, string? handle, DateTimeOffset issuedAt);
    public Task SendEmailUpdateAsync(string token, string to, string? handle, DateTimeOffset issuedAt);
    public Task SendPasswordResetAsync(string token, string to, string? handle, DateTimeOffset issuedAt);
    public Task SendPlcOperationSignatureAsync(string token, string to, string? handle, DateTimeOffset issuedAt);
}
