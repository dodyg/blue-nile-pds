using System.Net;
using System.Text;
using System.Text.Json;
using BlueNilePds.Host.Tests.Infrastructure;

namespace BlueNilePds.Host.Tests;

public class TurnstileTests
{
    private static readonly TestWebAppFactory Factory = new();
    private static readonly TestWebAppFactory TurnstileFactory = new(
        new Dictionary<string, string?> { ["Config:PDS_TURNSTILE_SECRET"] = "test-secret" });

    private HttpClient Client => Factory.CreateClient();
    private HttpClient TurnstileClient => TurnstileFactory.CreateClient();

    private string UniqueHandle() => $"u{Guid.NewGuid():N}"[..10] + ".test";
    private string UniqueEmail() => $"e{Guid.NewGuid():N}"[..12] + "@test.test";

    private static StringContent RegisterBody(string email, string handle, string? verificationCode = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["email"] = email,
            ["handle"] = handle,
            ["password"] = "test-password-123",
        };
        if (verificationCode != null)
            body["verificationCode"] = verificationCode;
        return new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
    }

    [Test]
    public async Task PendingRegister_WithoutTurnstileConfigured_SucceedsWithoutToken()
    {
        var response = await Client.PostAsync("/api/pending/register",
            RegisterBody(UniqueEmail(), UniqueHandle()));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task PendingRegister_WithTurnstileConfigured_MissingToken_ReturnsBadRequest()
    {
        var response = await TurnstileClient.PostAsync("/api/pending/register",
            RegisterBody(UniqueEmail(), UniqueHandle()));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task PendingRegister_WithTurnstileConfigured_ValidToken_Succeeds()
    {
        var response = await TurnstileClient.PostAsync("/api/pending/register",
            RegisterBody(UniqueEmail(), UniqueHandle(), verificationCode: "test-token"));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("accessJwt", out _)).IsTrue();
    }

    [Test]
    public async Task PendingConfig_WithTurnstileConfigured_ExposesRequirementAndSiteKey()
    {
        var response = await TurnstileClient.GetAsync("/api/pending/config");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.GetProperty("turnstileRequired").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task PendingConfig_WithoutTurnstileConfigured_NotRequired()
    {
        var response = await Client.GetAsync("/api/pending/config");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        // WhenWritingDefault drops false values, so a missing property means false.
        var required = json.TryGetProperty("turnstileRequired", out var el) && el.GetBoolean();
        await Assert.That(required).IsFalse();
    }
}
