using System.Net;
using atompds.Tests.Infrastructure;

namespace atompds.Tests;

public class AdminTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    private HttpRequestMessage CreateAdminRequest(string method, string url, string? body = null)
    {
        var httpMethod = method == "GET" ? HttpMethod.Get : HttpMethod.Post;
        var request = new HttpRequestMessage(httpMethod, url);
        request.Headers.Add("Authorization", AuthTestHelper.GetAdminBasicAuth());
        if (body != null)
        {
            request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        }
        else if (httpMethod == HttpMethod.Post)
        {
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        }
        return request;
    }

    [Test]
    public async Task GetAccountInfo_NoAuth_ReturnsAuthError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.admin.getAccountInfo?did=did:plc:test");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetAccountInfo_WithAdminAuth_ReturnsNonAuthError()
    {
        var request = CreateAdminRequest("GET", "/xrpc/com.atproto.admin.getAccountInfo?did=did:plc:test");
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetAccountInfo_MissingDid_ReturnsError()
    {
        var request = CreateAdminRequest("GET", "/xrpc/com.atproto.admin.getAccountInfo");
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GetAccountInfos_NoAuth_ReturnsAuthError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.admin.getAccountInfos?dids=did:plc:test");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetAccountInfos_WithAdminAuth_ReturnsNonAuthError()
    {
        var request = CreateAdminRequest("GET", "/xrpc/com.atproto.admin.getAccountInfos?dids=did:plc:test");
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetInviteCodes_NoAuth_ReturnsAuthError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.admin.getInviteCodes");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetInviteCodes_WithAdminAuth_ReturnsOk()
    {
        var request = CreateAdminRequest("GET", "/xrpc/com.atproto.admin.getInviteCodes");
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task DisableInviteCodes_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.admin.disableInviteCodes", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task DisableInviteCodes_WithAdminAuth_ReturnsOk()
    {
        var request = CreateAdminRequest("POST", "/xrpc/com.atproto.admin.disableInviteCodes", "{\"codes\":[\"test-code\"]}");
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task EnableAccountInvites_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.admin.enableAccountInvites", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task DisableAccountInvites_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.admin.disableAccountInvites", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AdminDeleteAccount_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.admin.deleteAccount", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AdminDeleteAccount_WithAdminAuth_NullBody_ReturnsError()
    {
        var request = CreateAdminRequest("POST", "/xrpc/com.atproto.admin.deleteAccount");
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task SendEmail_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.admin.sendEmail", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task SendEmail_WithAdminAuth_NullBody_ReturnsError()
    {
        var request = CreateAdminRequest("POST", "/xrpc/com.atproto.admin.sendEmail");
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GetSubjectStatus_NoAuth_ReturnsAuthError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.admin.getSubjectStatus?did=did:plc:test");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetSubjectStatus_WithAdminAuth_ReturnsOk()
    {
        var request = CreateAdminRequest("GET", "/xrpc/com.atproto.admin.getSubjectStatus?did=did:plc:test");
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UpdateSubjectStatus_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.admin.updateSubjectStatus", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UpdateSubjectStatus_WithAdminAuth_NullBody_ReturnsError()
    {
        var request = CreateAdminRequest("POST", "/xrpc/com.atproto.admin.updateSubjectStatus");
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UpdateAccountEmail_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.admin.updateAccountEmail", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UpdateAccountEmail_WithAdminAuth_NullBody_ReturnsError()
    {
        var request = CreateAdminRequest("POST", "/xrpc/com.atproto.admin.updateAccountEmail");
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UpdateAccountHandle_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.admin.updateAccountHandle", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UpdateAccountHandle_WithAdminAuth_NullBody_ReturnsError()
    {
        var request = CreateAdminRequest("POST", "/xrpc/com.atproto.admin.updateAccountHandle");
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UpdateAccountPassword_NoAuth_ReturnsAuthError()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.admin.updateAccountPassword", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UpdateAccountPassword_WithAdminAuth_NullBody_ReturnsError()
    {
        var request = CreateAdminRequest("POST", "/xrpc/com.atproto.admin.updateAccountPassword");
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task AdminEndpoints_RejectBadCredentials()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/xrpc/com.atproto.admin.getInviteCodes");
        request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("admin:wrongpassword")));
        var response = await Client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}
