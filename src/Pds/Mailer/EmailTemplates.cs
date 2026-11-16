using System.Globalization;
using System.Net;
using System.Text;

namespace BlueNilePds.Pds.Mailer;

public enum EmailPurpose
{
    ConfirmEmail,
    EmailUpdate,
    PasswordReset,
    AccountDelete,
    PlcOperation,
}

public record RenderedEmail(string Subject, string Html, string Text);

/// <summary>
/// The account emails. Each carries the code only, never a link: a PDS serves accounts for any app, so it can't know which page
/// should take the code. The only setting they use is PDS_SERVICE_NAME.
/// </summary>
public static class EmailTemplates
{
    public const string Accent = "#1f2937";

    // The same lifetime the token stores check (EmailTokenStore, PendingEmailTokenStore).
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);

    private sealed record Copy(string CodeName, string Heading, string Lead, string IfNotYou);

    /// <param name="issuedAt">When the code was made. The expiry is counted from here, not from when the email goes out.</param>
    public static RenderedEmail Render(EmailPurpose purpose, string code, string? handle, string serviceName, DateTimeOffset issuedAt)
    {
        var account = "your account";
        if (!string.IsNullOrWhiteSpace(handle))
        {
            account = handle.Trim();
        }

        var copy = CopyFor(purpose, account);
        var displayCode = code.Trim().ToUpperInvariant();
        var expires = issuedAt.ToUniversalTime().Add(CodeLifetime);
        var expiry = $"The code expires in {CodeLifetime.TotalMinutes:0} minutes, at {expires.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture)} UTC.";

        // The code comes first so it still shows when a notification cuts the subject short; the account tells apart codes for accounts that share an inbox.
        var subject = $"{displayCode} is your {serviceName} {copy.CodeName}";
        if (!string.IsNullOrWhiteSpace(handle))
        {
            subject += $" for {handle.Trim()}";
        }

        var footer = $"Sent by {serviceName} about {account}.";
        var text = new StringBuilder()
            .AppendLine(copy.Heading)
            .AppendLine()
            .AppendLine(copy.Lead)
            .AppendLine()
            .AppendLine(displayCode)
            .AppendLine()
            .AppendLine(expiry)
            .AppendLine()
            .AppendLine(copy.IfNotYou)
            .AppendLine()
            .AppendLine("-- ")
            .AppendLine(footer);

        var body = new StringBuilder()
            .Append(Paragraph(copy.Lead, "16px", "#111827"))
            .Append(CodeBlock(displayCode))
            .Append(Paragraph(expiry, "14px", "#4b5563"))
            .Append(Paragraph(copy.IfNotYou, "14px", "#4b5563"));
        var html = Layout(copy.Heading, body.ToString(), footer, serviceName);
        return new RenderedEmail(subject, html, text.ToString());
    }

    public static RenderedEmail RenderCustom(string subject, string content, string? handle, string serviceName)
    {
        var footer = $"Sent by {serviceName}.";
        if (!string.IsNullOrWhiteSpace(handle))
        {
            footer = $"Sent by {serviceName} about {handle.Trim()}.";
        }

        var paragraphs = content.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(p => Paragraph(p.Trim(), "16px", "#111827").Replace("&#10;", "<br />"));
        var html = Layout(subject, string.Concat(paragraphs), footer, serviceName);
        var text = $"{content.TrimEnd()}\n\n-- \n{footer}\n";
        return new RenderedEmail(subject, html, text);
    }

    private static Copy CopyFor(EmailPurpose purpose, string account) => purpose switch
    {
        EmailPurpose.ConfirmEmail => new Copy(
            "confirmation code",
            "Confirm your email",
            $"Use this code to confirm the email address for {account}.",
            "If you didn't create an account or add this email address, ignore this message. Nothing changes unless the code is used."),
        EmailPurpose.EmailUpdate => new Copy(
            "email change code",
            "Confirm your email change",
            $"Use this code to change the email address of {account}.",
            "If you didn't ask to change your email, ignore this message and your email stays the same. If you think someone else knows your password, change it."),
        EmailPurpose.PasswordReset => new Copy(
            "password reset code",
            "Reset your password",
            $"Use this code to reset the password for {account}.",
            "If you didn't ask to reset your password, ignore this message and your password stays the same."),
        EmailPurpose.AccountDelete => new Copy(
            "account deletion code",
            "Confirm account deletion",
            $"Use this code to permanently delete {account}.",
            "If you didn't ask to delete your account, ignore this message and nothing is deleted. Then change your password, since someone else may be signed in."),
        EmailPurpose.PlcOperation => new Copy(
            "identity change code",
            "Confirm an identity change",
            $"Use this code to approve a change to the identity (DID document) of {account}.",
            "If you didn't ask for this, don't share the code with anyone. Ignore this message and change your password."),
        _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null),
    };

    private static string E(string value) => WebUtility.HtmlEncode(value);

    private static string Paragraph(string value, string size, string color) =>
        $"<p style=\"margin:0 0 16px;font-size:{size};line-height:1.5;color:{color};\">{E(value).Replace("\n", "&#10;")}</p>";

    private static string CodeBlock(string code) =>
        "<div style=\"margin:0 0 16px;padding:14px 16px;background:#f3f4f6;border:1px solid #e5e7eb;border-radius:8px;" +
        "font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace;font-size:24px;font-weight:700;letter-spacing:0.12em;color:#111827;text-align:center;\">" +
        $"{E(code)}</div>";

    private static string Layout(string heading, string body, string footer, string serviceName)
    {
        var mark = $"<div style=\"display:inline-block;width:32px;height:32px;line-height:32px;border-radius:8px;background:{Accent};" +
            $"color:#ffffff;font-weight:700;font-size:16px;text-align:center;vertical-align:middle;\">{E(Initial(serviceName))}</div>";
        return "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\" /><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\" />" +
            "<meta name=\"color-scheme\" content=\"light\" /><meta name=\"supported-color-schemes\" content=\"light\" /></head>" +
            "<body style=\"margin:0;padding:0;background:#f9fafb;\">" +
            "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f9fafb;\"><tr><td align=\"center\" style=\"padding:32px 16px;\">" +
            "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:520px;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;\">" +
            $"<tr><td style=\"padding:0 0 16px;\">{mark}<span style=\"margin-left:10px;font-size:15px;font-weight:600;color:#111827;vertical-align:middle;\">{E(serviceName)}</span></td></tr>" +
            "<tr><td style=\"background:#ffffff;border:1px solid #e5e7eb;border-radius:12px;padding:28px 24px;\">" +
            $"<h1 style=\"margin:0 0 16px;font-size:22px;line-height:1.3;color:#111827;\">{E(heading)}</h1>{body}</td></tr>" +
            $"<tr><td style=\"padding:16px 4px 0;font-size:12px;line-height:1.5;color:#6b7280;\">{E(footer)}</td></tr>" +
            "</table></td></tr></table></body></html>";
    }

    private static string Initial(string serviceName)
    {
        var trimmed = serviceName.Trim();
        if (trimmed.Length == 0)
        {
            return "P";
        }

        return char.ToUpperInvariant(trimmed[0]).ToString();
    }
}
