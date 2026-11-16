using BlueNilePds.Host.Services;
using BlueNilePds.Pds.Mailer;
using MimeKit;

namespace BlueNilePds.Host.Tests;

public class EmailTemplatesTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    private const string Service = "Example";

    [Test]
    public async Task Render_PutsTheCodeFirstInTheSubjectThenTheServiceAndTheAccount()
    {
        var email = EmailTemplates.Render(EmailPurpose.ConfirmEmail, "abcde-fghij", "alice.pds.test", Service, IssuedAt);

        await Assert.That(email.Subject).IsEqualTo("ABCDE-FGHIJ is your Example confirmation code for alice.pds.test");
        await Assert.That(email.Text).Contains("ABCDE-FGHIJ");
        await Assert.That(email.Html).Contains("ABCDE-FGHIJ");
        await Assert.That(email.Html).DoesNotContain("abcde-fghij");
    }

    [Test]
    public async Task Render_WithoutAHandle_LeavesTheAccountOutOfTheSubjectAndSaysYourAccount()
    {
        var email = EmailTemplates.Render(EmailPurpose.ConfirmEmail, "abcde-fghij", null, Service, IssuedAt);

        await Assert.That(email.Subject).IsEqualTo("ABCDE-FGHIJ is your Example confirmation code");
        await Assert.That(email.Text).Contains("confirm the email address for your account.");
    }

    [Test]
    public async Task Render_NamesTheAccountAndCountsTheExpiryFromWhenTheCodeWasMade()
    {
        var email = EmailTemplates.Render(EmailPurpose.PasswordReset, "abcde-fghij", "blog.pds.test", Service, IssuedAt);

        await Assert.That(email.Subject).IsEqualTo("ABCDE-FGHIJ is your Example password reset code for blog.pds.test");
        await Assert.That(email.Text).Contains("Use this code to reset the password for blog.pds.test.");
        await Assert.That(email.Text).Contains("The code expires in 15 minutes, at 10 Nov 2026, 09:15 UTC.");
        await Assert.That(email.Html).Contains("09:15 UTC");
        await Assert.That(email.Text).Contains("ignore this message and your password stays the same");
    }

    [Test]
    [Arguments(EmailPurpose.ConfirmEmail)]
    [Arguments(EmailPurpose.EmailUpdate)]
    [Arguments(EmailPurpose.PasswordReset)]
    [Arguments(EmailPurpose.AccountDelete)]
    [Arguments(EmailPurpose.PlcOperation)]
    public async Task Render_EveryPurpose_CarriesTheCodeAndNoLink(EmailPurpose purpose)
    {
        var email = EmailTemplates.Render(purpose, "abcde-fghij", "alice.pds.test", Service, IssuedAt);

        await Assert.That(email.Html).DoesNotContain("<a ");
        await Assert.That(email.Html).DoesNotContain("href");
        await Assert.That(email.Text).DoesNotContain("http");
        await Assert.That(email.Text).Contains("ABCDE-FGHIJ");
    }

    [Test]
    public async Task Render_IsInEnglishAndTheTextPartEndsWithAStandardSignature()
    {
        var email = EmailTemplates.Render(EmailPurpose.ConfirmEmail, "abcde-fghij", "alice.pds.test", Service, IssuedAt);
        var custom = EmailTemplates.RenderCustom("Subject", "Body", "alice.pds.test", Service);

        await Assert.That(email.Html).Contains("<html lang=\"en\">");
        await Assert.That(email.Text.Replace("\r\n", "\n")).Contains("\n-- \nSent by Example about alice.pds.test.");
        await Assert.That(custom.Text).Contains("\n-- \nSent by Example about alice.pds.test.");
    }

    [Test]
    public async Task Render_EncodesTheHandleAndServiceName()
    {
        var email = EmailTemplates.Render(EmailPurpose.ConfirmEmail, "abcde-fghij", "<b>x</b>", "A&B <PDS>", IssuedAt);

        await Assert.That(email.Html).DoesNotContain("<b>x</b>");
        await Assert.That(email.Html).Contains("&lt;b&gt;x&lt;/b&gt;");
        await Assert.That(email.Html).Contains("A&amp;B &lt;PDS&gt;");
    }

    [Test]
    public async Task Render_HeaderUsesTheFixedAccentAndTheServiceInitial()
    {
        var email = EmailTemplates.Render(EmailPurpose.ConfirmEmail, "c", "a.test", Service, IssuedAt);

        await Assert.That(email.Html).Contains($"background:{EmailTemplates.Accent};");
        await Assert.That(email.Html).Contains(">E</div>");
        await Assert.That(email.Html).DoesNotContain("<img");
    }

    [Test]
    public async Task BuildMessage_IsMultipartAlternativeWithTheSenderName()
    {
        var email = EmailTemplates.Render(EmailPurpose.ConfirmEmail, "abcde-fghij", "alice.pds.test", Service, IssuedAt);

        using var message = SmtpMailer.BuildMessage(email, "alice@example.test", "Example", "noreply@pds.test");

        var body = message.Body as Multipart;
        await Assert.That(body).IsNotNull();
        await Assert.That(body!.ContentType.MimeType).IsEqualTo("multipart/alternative");
        await Assert.That(message.TextBody).Contains("ABCDE-FGHIJ");
        await Assert.That(message.HtmlBody).Contains("ABCDE-FGHIJ");
        await Assert.That(((MailboxAddress)message.From[0]).Name).IsEqualTo("Example");
    }

    [Test]
    public async Task RenderCustom_KeepsTheTextAndNamesTheAccount()
    {
        var email = EmailTemplates.RenderCustom("Your account has been approved", "Hello alice,\n\nYou're in.", "alice.test", Service);

        await Assert.That(email.Subject).IsEqualTo("Your account has been approved");
        await Assert.That(email.Text).Contains("Hello alice,");
        await Assert.That(email.Html).Contains("You&#39;re in.");
        await Assert.That(email.Text).Contains("Sent by Example about alice.test.");
    }

    [Test]
    public async Task Dispatcher_TakesTheIssueTimeWhenQueuing_NotWhenTheQueueGetsToIt()
    {
        var mailer = new RecordingMailer();
        var queue = new HeldQueue();
        var time = new SteppedTime(IssuedAt);
        var dispatcher = new BackgroundEmailDispatcher(mailer, queue, time);

        await dispatcher.SendPasswordResetAsync("abcde-fghij", "alice@example.test", "alice.pds.test");
        time.Now = IssuedAt.AddMinutes(20);
        await queue.RunAllAsync();

        await Assert.That(mailer.IssuedAt).IsEqualTo(IssuedAt);
        await Assert.That(mailer.Handle).IsEqualTo("alice.pds.test");
    }

    private sealed class SteppedTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class HeldQueue : IBackgroundJobQueue
    {
        private readonly List<Func<IServiceProvider, Task>> _jobs = [];

        public ValueTask EnqueueAsync(Func<IServiceProvider, Task> job, CancellationToken ct = default)
        {
            _jobs.Add(job);
            return ValueTask.CompletedTask;
        }

        public async Task RunAllAsync()
        {
            foreach (var job in _jobs)
            {
                await job(null!);
            }
        }
    }

    private sealed class RecordingMailer : IMailer
    {
        public DateTimeOffset? IssuedAt { get; private set; }
        public string? Handle { get; private set; }

        public Task SendCustomEmailAsync(string subject, string content, string to, string? handle) => Task.CompletedTask;
        public Task SendAccountDeleteAsync(string token, string to, string? handle, DateTimeOffset issuedAt) => Record(handle, issuedAt);
        public Task SendEmailConfirmationAsync(string token, string to, string? handle, DateTimeOffset issuedAt) => Record(handle, issuedAt);
        public Task SendEmailUpdateAsync(string token, string to, string? handle, DateTimeOffset issuedAt) => Record(handle, issuedAt);
        public Task SendPasswordResetAsync(string token, string to, string? handle, DateTimeOffset issuedAt) => Record(handle, issuedAt);
        public Task SendPlcOperationSignatureAsync(string token, string to, string? handle, DateTimeOffset issuedAt) => Record(handle, issuedAt);

        private Task Record(string? handle, DateTimeOffset issuedAt)
        {
            Handle = handle;
            IssuedAt = issuedAt;
            return Task.CompletedTask;
        }
    }
}
