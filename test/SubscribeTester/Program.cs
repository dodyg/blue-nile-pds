using System.Text.Json;
using AppBsky.Embed;
using AppBsky.Feed;
using AppBsky.Graph;
using CarpaNet;
using CommonWeb.Generated;
using CarpaNet.Json;
using ComAtproto.Repo;
using ComAtproto.Sync;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
});

var log = loggerFactory.CreateLogger("Debug");

var atProtocol = ATProtoClientFactory.Create(new ATProtoClientOptions
{
    BaseUrl = new Uri("https://pds.ramen.fyi"),
    LoggerFactory = loggerFactory
});

await foreach (var message in atProtocol.ComAtprotoSyncSubscribeReposAsync())
{
    await HandleMessageAsync(message);
}

async Task HandleMessageAsync(ISubscribeReposMessage message)
{
    if (message is not SubscribeReposCommit commit)
    {
        return;
    }

    var orgId = commit.Repo;

    var repoOutput = await atProtocol.ComAtprotoRepoDescribeRepoAsync(new DescribeRepoParameters { Repo = orgId.Value });

    foreach (var op in commit.Ops)
    {
        if (!string.Equals(op.Action, "create", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(op.Action, "update", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var pathParts = op.Path.Split('/');
        if (pathParts.Length != 2)
        {
            continue;
        }

        var getRecord = await atProtocol.ComAtprotoRepoGetRecordAsync(new GetRecordParameters
        {
            Repo = orgId.Value,
            Collection = pathParts[0],
            Rkey = pathParts[1]
        });

        log.LogInformation("Record: {Record}", getRecord.Value.GetRawText());

        if (pathParts[0] == Follow.RecordType)
        {
            var follow = JsonSerializer.Deserialize<Follow>(getRecord.Value, ATProtoJsonContext.DefaultOptions);
            if (follow != null)
            {
                log.LogInformation("Follow: {Subject} -> {CreatedAt}", follow.Subject, follow.CreatedAt);
            }
        }

        if (pathParts[0] == Post.RecordType)
        {
            var post = JsonSerializer.Deserialize<Post>(getRecord.Value, ATProtoJsonContext.DefaultOptions);
            if (post == null)
            {
                continue;
            }

            var did = commit.Repo;
            var url = $"https://bsky.app/profile/{did}/post/{pathParts[1]}";
            log.LogInformation("Post URL: {Url}, from {Handle}", url, repoOutput.Handle);

            if (post.Reply is not null)
            {
                log.LogInformation("Reply Root: {Root}, Parent: {Parent}", post.Reply.Root, post.Reply.Parent);
            }

            if (post.Embed is Video videoEmbed)
            {
                var linkString = videoEmbed.VideoValue.Ref.Value;
                log.LogInformation("Video Link: https://video.bsky.app/watch/{Did}/{LinkString}/playlist.m3u8", did, linkString);
            }
        }
    }
}
