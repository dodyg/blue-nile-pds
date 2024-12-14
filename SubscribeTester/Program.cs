using System.Collections.Concurrent;
using FishyFlip;
using FishyFlip.Lexicon.App.Bsky.Embed;
using FishyFlip.Lexicon.App.Bsky.Feed;
using FishyFlip.Lexicon.App.Bsky.Graph;
using FishyFlip.Lexicon.Com.Atproto.Repo;
using FishyFlip.Models;
using FishyFlip.Tools;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
});

var log = loggerFactory.CreateLogger("Debug");

var atWebProtocol = new ATWebSocketProtocolBuilder()
    .WithInstanceUrl(new Uri("https://pds.ramen.fyi"))
    .WithLogger(log)
    .Build();

var atProtocol = new ATProtocolBuilder()
    .WithInstanceUrl(new Uri("https://pds.ramen.fyi"))
    .WithLogger(log)
    .Build();


var messageQueue = new ConcurrentQueue<SubscribeRepoMessage>();

atWebProtocol.OnSubscribedRepoMessage += (sender, args) =>
{
    messageQueue.Enqueue(args.Message);
};

await atWebProtocol.StartSubscribeReposAsync();

while (true)
{
    if (messageQueue.TryDequeue(out var message))
    {
        await HandleMessageAsync(message);
    }
    else
    {
        await Task.Delay(1000);
    }
}

async Task HandleMessageAsync(SubscribeRepoMessage message)
{
    if (message.Commit is null)
    {
        return;
    }
    
    var orgId = message.Commit.Repo;

    if (orgId is null)
    {
        return;
    }

    if (message.Record is not null)
    {
        log.LogInformation("Record: {Record}", message.Record.ToJson());
        
        
        if (message.Record is Follow follow)
        {
            log.LogInformation("Follow: {Subject} -> {CreatedAt}", follow.Subject, follow.CreatedAt);
        }
        
        if (message.Record is Post post)
        {
            // The Actor Did.
            var did = message.Commit.Repo;

            var repo = (await atProtocol.DescribeRepoAsync(did)).HandleResult();

            // Commit.Ops are the actions used when creating the message.
            // In this case, it's a create record for the post.
            // The path contains the post action and path, we need the path, so we split to get it.
            var url = $"https://bsky.app/profile/{did}/post/{message.Commit.Ops![0]!.Path!.Split('/').Last()}";
            log.LogInformation("Post URL: {Url}, from {Handle}", url, repo?.Handle);

            if (post.Reply is not null)
            {
                log.LogInformation("Reply Root: {Root}, Parent: {Parent}", post.Reply.Root, post.Reply.Parent);
            }

            if (post.Embed is EmbedVideo videoEmbed)
            {
                // https://video.bsky.app/watch/did%3Aplc%3Acxe5e4ldjfvryf5dqvopdq3v/bafkreiefakrdmclohastskuauwurbtx3tnu2drjpnirsoroyalq5nqr73a/playlist.m3u8
                var link = videoEmbed.Video?.Ref?.Link;
                var linkString = link?.ToString();
                log.LogInformation("Video Link: https://video.bsky.app/watch/{Did}/{LinkString}/playlist.m3u8", did, linkString);
            }
        }
    }
}

public static class TaskExtensions
{
    public static void FireAndForget(this Task task, Action<Exception> errorHandler = null)
    {
        if (task == null)
            throw new ArgumentNullException(nameof(task));

        task.ContinueWith(t =>
        {
            if (errorHandler != null && t.IsFaulted)
                errorHandler(t.Exception);
        }, TaskContinuationOptions.OnlyOnFaulted);

        // Avoiding warning about not awaiting the fire-and-forget task.
        // However, since the method is intended to fire and forget, we don't actually await it.
#pragma warning disable CS4014
        task.ConfigureAwait(false);
#pragma warning restore CS4014
    }
}