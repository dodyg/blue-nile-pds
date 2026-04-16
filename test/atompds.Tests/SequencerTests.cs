using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using atompds.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace atompds.Tests;

public class SequencerTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    private string UniqueHandle() => $"u{Guid.NewGuid():N}"[..10] + ".test";
    private string UniqueEmail() => $"e{Guid.NewGuid():N}"[..12] + "@test.test";

    private async Task<AccountInfo> CreateAccountAsync()
    {
        return await AccountHelper.CreateAccountAsync(Client, handle: UniqueHandle(), email: UniqueEmail());
    }

    private async Task CreatePostAsync(AccountInfo account)
    {
        var body = new Dictionary<string, object?>
        {
            ["repo"] = account.Did,
            ["collection"] = "app.bsky.feed.post",
            ["record"] = new Dictionary<string, object?>
            {
                ["$type"] = "app.bsky.feed.post",
                ["text"] = $"Seq test {Guid.NewGuid():N}"[..15],
                ["createdAt"] = DateTime.UtcNow.ToString("o")
            }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.createRecord")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessJwt);
        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private async Task<List<Sequencer.Db.RepoSeq>> GetSequencerEventsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<Sequencer.Db.SequencerDb>>();
        using var db = await factory.CreateDbContextAsync();
        return await db.RepoSeqs.OrderBy(e => e.Seq).ToListAsync();
    }

    [Test]
    public async Task Sequencer_EventsCreated()
    {
        await CreateAccountAsync();

        var events = await GetSequencerEventsAsync();
        await Assert.That(events.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Sequencer_AccountEvent()
    {
        await CreateAccountAsync();

        var events = await GetSequencerEventsAsync();
        var accountEvents = events.Where(e => e.EventType == Sequencer.Db.RepoSeqEventType.Account).ToList();
        await Assert.That(accountEvents.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Sequencer_CommitEvent()
    {
        var account = await CreateAccountAsync();
        await CreatePostAsync(account);

        var events = await GetSequencerEventsAsync();
        var commitEvents = events.Where(e => e.EventType == Sequencer.Db.RepoSeqEventType.Append).ToList();
        await Assert.That(commitEvents.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Sequencer_IdentityEvent()
    {
        await CreateAccountAsync();

        var events = await GetSequencerEventsAsync();
        var identityEvents = events.Where(e => e.EventType == Sequencer.Db.RepoSeqEventType.Identity).ToList();
        await Assert.That(identityEvents.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Sequencer_CursorIncreases()
    {
        var account = await CreateAccountAsync();
        await CreatePostAsync(account);
        await CreatePostAsync(account);

        var events = await GetSequencerEventsAsync();
        await Assert.That(events.Count).IsGreaterThan(1);
        for (int i = 1; i < events.Count; i++)
        {
            await Assert.That(events[i].Seq).IsGreaterThan(events[i - 1].Seq);
        }
    }
}
