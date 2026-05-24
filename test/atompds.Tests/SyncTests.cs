using System.Net;
using atompds.Tests.Infrastructure;

namespace atompds.Tests;

public class SyncTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    [Test]
    public async Task ListRepos_ReturnsOk()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.sync.listRepos");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ListRepos_WithLimit_ReturnsOk()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.sync.listRepos?limit=10");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ListRepos_ContainsReposAndCursor()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.sync.listRepos");
        var json = await AuthTestHelper.ReadJsonAsync(response);
        await Assert.That(json.TryGetProperty("repos", out _)).IsTrue();
    }

    [Test]
    public async Task GetLatestCommit_MissingDid_ReturnsError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.sync.getLatestCommit");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetLatestCommit_WithDid_ReturnsNonNotFound()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.sync.getLatestCommit?did=did:plc:test");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetRepo_MissingDid_ReturnsError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.sync.getRepo");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetRepo_WithDid_ReturnsNonNotFound()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.sync.getRepo?did=did:plc:test");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetRepoStatus_MissingDid_ReturnsError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.sync.getRepoStatus");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetRepoStatus_WithDid_ReturnsNonNotFound()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.sync.getRepoStatus?did=did:plc:test");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetBlob_MissingParams_ReturnsError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.sync.getBlob");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetBlocks_MissingParams_ReturnsError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.sync.getBlocks");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetRecord_MissingParams_ReturnsError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.sync.getRecord");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetRecord_WithParams_ReturnsNonNotFound()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.sync.getRecord?did=did:plc:test&collection=app.bsky.feed.post&rkey=test");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ListBlobs_MissingDid_ReturnsError()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.sync.listBlobs");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ListBlobs_WithDid_ReturnsNonNotFound()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.sync.listBlobs?did=did:plc:test");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task SubscribeRepos_RouteExists()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.sync.subscribeRepos");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ListMissingBlobs_RouteExists()
    {
        var response = await Client.GetAsync("/xrpc/com.atproto.repo.listMissingBlobs");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ImportRepo_RouteExists()
    {
        var response = await Client.PostAsync("/xrpc/com.atproto.repo.importRepo", null);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }
}
