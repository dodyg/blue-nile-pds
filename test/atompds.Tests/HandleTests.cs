using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using atompds.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace atompds.Tests;

public class HandleTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    private string UniqueHandle() => $"u{Guid.NewGuid():N}"[..10] + ".test";
    private string UniqueEmail() => $"e{Guid.NewGuid():N}"[..12] + "@test.test";

    [Test]
    public async Task ResolveHandle_ByHandle_ReturnsDid()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var response = await Client.GetAsync($"/xrpc/com.atproto.identity.resolveHandle?handle={account.Handle}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.GetProperty("did").GetString()).IsEqualTo(account.Did);
    }

    [Test]
    public async Task ResolveHandle_UnknownHandle_ReturnsError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.identity.resolveHandle?handle=nonexistent.test");

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task UpdateHandle_Succeeds()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());
        var newHandle = UniqueHandle();

        var body = new Dictionary<string, object?> { ["handle"] = newHandle };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.identity.updateHandle")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task UpdateHandle_InvalidHandle_ReturnsError()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var body = new Dictionary<string, object?> { ["handle"] = "x" };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.identity.updateHandle")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UpdateHandle_TakenHandle_ReturnsError()
    {
        var handle1 = UniqueHandle();
        var handle2 = UniqueHandle();
        await AccountHelper.CreateAccountAsync(Client, handle: handle1, email: UniqueEmail());
        var account2 = await AccountHelper.CreateAccountAsync(Client, handle: handle2, email: UniqueEmail());

        var body = new Dictionary<string, object?> { ["handle"] = handle1 };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.identity.updateHandle")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account2.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetRecommendedDidCredentials_ReturnsInfo()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var request = new HttpRequestMessage(HttpMethod.Get, "/xrpc/com.atproto.identity.getRecommendedDidCredentials");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("rotationKeys", out _)).IsTrue();
        await Assert.That(json.TryGetProperty("signingKey", out _)).IsTrue();
    }

    [Test]
    public async Task RequestPlcOperationSignature_Succeeds()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.identity.requestPlcOperationSignature");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task SignPlcOperation_ReturnsOperation()
    {
        var account = await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());

        var body = new Dictionary<string, object?> { ["operation"] = new Dictionary<string, object?> { ["prev"] = "test-prev" } };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.identity.signPlcOperation")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }
}
