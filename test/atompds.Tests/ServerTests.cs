using System.Net;
using System.Text.Json;
using atompds.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace atompds.Tests;

public class ServerTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    [Test]
    public async Task DescribeServer_ReturnsOk()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.server.describeServer");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task DescribeServer_ContainsDid()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.server.describeServer");
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("did", out _)).IsTrue();
    }

    [Test]
    public async Task DescribeServer_ContainsAvailableUserDomains()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.server.describeServer");
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("availableUserDomains", out _)).IsTrue();
    }

    [Test]
    public async Task DescribeServer_ContainsInviteCodeRequired()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.server.describeServer");
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("inviteCodeRequired", out _)).IsTrue();
    }

    [Test]
    public async Task CreateSession_MissingCredentials_ReturnsError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.server.createSession",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateSession_NullBody_ReturnsError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.server.createSession",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GetSession_NoAuth_ReturnsAuthError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.server.getSession");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetSession_WithValidToken_ReturnsNonAuthError()
    {
        var token = AuthTestHelper.CreateAccessToken();
        var request = new HttpRequestMessage(HttpMethod.Get, "/xrpc/com.atproto.server.getSession");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RefreshSession_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.server.refreshSession", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RefreshSession_WithAccessToken_ReturnsAuthError()
    {
        var token = AuthTestHelper.CreateAccessToken();
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.refreshSession");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task DeleteSession_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.server.deleteSession", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CreateAccount_NullBody_ReturnsError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.server.createAccount",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task RequestAccountDelete_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.server.requestAccountDelete", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task DeleteAccount_NullBody_ReturnsError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.server.deleteAccount",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ActivateAccount_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.server.activateAccount", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task DeactivateAccount_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.server.deactivateAccount", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CheckAccountStatus_NoAuth_ReturnsAuthError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.server.checkAccountStatus");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CreateAppPassword_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.server.createAppPassword", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ListAppPasswords_NoAuth_ReturnsAuthError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.server.listAppPasswords");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RevokeAppPassword_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.server.revokeAppPassword", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CreateInviteCode_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.server.createInviteCode", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task CreateInviteCodes_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.server.createInviteCodes", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetAccountInviteCodes_NoAuth_ReturnsAuthError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.server.getAccountInviteCodes");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ConfirmEmail_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.server.confirmEmail", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RequestEmailConfirmation_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.server.requestEmailConfirmation", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RequestEmailUpdate_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.server.requestEmailUpdate", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RequestPasswordReset_NullBody_ReturnsError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.server.requestPasswordReset",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ResetPassword_NullBody_ReturnsError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.server.resetPassword",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UpdateEmail_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.server.updateEmail", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ReserveSigningKey_NullBody_ReturnsOk()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.server.reserveSigningKey",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetServiceAuth_NoAuth_ReturnsAuthError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.server.getServiceAuth");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetServiceAuth_WithValidToken_ReturnsResponse()
    {
        var token = AuthTestHelper.CreateAccessToken();
        var request = new HttpRequestMessage(HttpMethod.Get, "/xrpc/com.atproto.server.getServiceAuth?aud=did:web:test.com");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }
}
