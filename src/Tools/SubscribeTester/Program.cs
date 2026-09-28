using CarpaNet;
using CarpaNet.Repo;
using ComAtproto.Sync;
using Microsoft.Extensions.Logging;
using PeterO.Cbor;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
});

var log = loggerFactory.CreateLogger("Firehose");

if (!TryParseArgs(args, out var baseUrl, out var cursor, out var kinds, out var showPayload))
{
    Console.WriteLine("Usage: SubscribeTester <pds-url> [--cursor <seq>] [--kinds commit,sync,identity,account,info] [--payload]");
    Console.WriteLine("  <pds-url>  Base URL of the PDS, e.g. https://bsky.africa (default)");
    Console.WriteLine("  --payload  Log the record JSON for create/update ops (decoded from the commit blocks)");
    return 1;
}

var client = ATProtoClientFactory.Create(new ATProtoClientOptions
{
    BaseUrl = baseUrl,
    LoggerFactory = loggerFactory,
    JsonOptions = ATProtoClientFactory.CreateJsonOptions(),
    CborContext = CarpaNet.Cbor.ATProtoCborContext.Default
});

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

log.LogInformation("Subscribing to {BaseUrl} (cursor: {Cursor}, kinds: {Kinds}, payload: {Payload}). Press Ctrl+C to stop.",
    baseUrl, cursor?.ToString() ?? "(live)", string.Join(",", kinds), showPayload);

long? resumeFrom = cursor;
var attempt = 0;
while (!cts.IsCancellationRequested)
{
    try
    {
        var parameters = resumeFrom.HasValue ? new SubscribeReposParameters { Cursor = resumeFrom.Value } : null;
        await foreach (var message in client.ComAtprotoSyncSubscribeReposAsync(parameters, cts.Token))
        {
            attempt = 0;
            HandleMessage(message, kinds, showPayload, log, ref resumeFrom);
        }

        log.LogInformation("Stream ended by server; reconnecting...");
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested)
    {
        break;
    }
    catch (Exception ex)
    {
        attempt++;
        var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
        log.LogWarning(ex, "Disconnected; reconnecting in {Delay}s (resume from {Cursor})...",
            delay.TotalSeconds, resumeFrom?.ToString() ?? "(live)");
    }

    if (cts.IsCancellationRequested)
    {
        break;
    }

    try
    {
        var attemptDelay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Max(attempt, 1))));
        await Task.Delay(attemptDelay, cts.Token);
    }
    catch (OperationCanceledException)
    {
        break;
    }
}

log.LogInformation("Stopped. Last cursor: {Cursor}", resumeFrom?.ToString() ?? "(none)");
return 0;

static bool TryParseArgs(string[] args, out Uri baseUrl, out long? cursor, out HashSet<string> kinds, out bool showPayload)
{
    baseUrl = new Uri("https://bsky.africa");
    cursor = null;
    showPayload = false;
    kinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "commit", "sync", "identity", "account", "info" };

    for (var i = 0; i < args.Length; i++)
    {
        if ((args[i] == "--cursor" || args[i] == "-c") && i + 1 < args.Length && long.TryParse(args[i + 1], out var c))
        {
            cursor = c;
            i++;
        }
        else if (args[i] == "--payload")
        {
            showPayload = true;
        }
        else if (args[i] == "--kinds" && i + 1 < args.Length)
        {
            kinds = new HashSet<string>(
                args[i + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                StringComparer.OrdinalIgnoreCase);
            if (kinds.Count == 0)
            {
                return false;
            }
            i++;
        }
        else if (args[i] == "--help" || args[i] == "-h")
        {
            return false;
        }
        else if (!args[i].StartsWith('-'))
        {
            if (!Uri.TryCreate(args[i], UriKind.Absolute, out var url) ||
                (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
            {
                return false;
            }
            baseUrl = url;
        }
        else
        {
            return false;
        }
    }

    return true;
}

static void HandleMessage(ISubscribeReposMessage message, HashSet<string> kinds, bool showPayload, ILogger log, ref long? cursor)
{
    switch (message)
    {
        case SubscribeReposCommit commit:
            cursor = commit.Seq;
            if (!kinds.Contains("commit"))
            {
                break;
            }
            log.LogInformation("[{Seq}] commit repo={Repo} rev={Rev} ops={Count}{Rebase}{TooBig}",
                commit.Seq, commit.Repo, commit.Rev, commit.Ops?.Count ?? 0,
                commit.Rebase ? " rebase" : "", commit.TooBig ? " tooBig" : "");
            var blocks = showPayload ? DecodeBlocks(commit, log) : null;
            if (commit.Ops != null)
            {
                foreach (var op in commit.Ops)
                {
                    log.LogInformation("    {Action} {Path}", op.Action, op.Path);
                    LogOpPayload(op, blocks, showPayload, log);
                }
            }
            break;

        case SubscribeReposSync sync:
            cursor = sync.Seq;
            if (kinds.Contains("sync"))
            {
                log.LogInformation("[{Seq}] sync did={Did} rev={Rev}", sync.Seq, sync.Did, sync.Rev);
            }
            break;

        case SubscribeReposIdentity identity:
            cursor = identity.Seq;
            if (kinds.Contains("identity"))
            {
                log.LogInformation("[{Seq}] identity did={Did} handle={Handle}", identity.Seq, identity.Did, identity.Handle);
            }
            break;

        case SubscribeReposAccount account:
            cursor = account.Seq;
            if (kinds.Contains("account"))
            {
                log.LogInformation("[{Seq}] account did={Did} active={Active} status={Status}",
                    account.Seq, account.Did, account.Active, account.Status);
            }
            break;

        case SubscribeReposInfo info:
            if (kinds.Contains("info"))
            {
                log.LogInformation("[info] {Name}: {Message}", info.Name, info.Message);
            }
            break;

        default:
            log.LogInformation("[unknown] {Type}", message.GetType().Name);
            break;
    }
}

static Dictionary<string, byte[]>? DecodeBlocks(SubscribeReposCommit commit, ILogger log)
{
    if (commit.Blocks == null || commit.Blocks.Length == 0)
    {
        return null;
    }

    try
    {
        using var reader = new CarReader(commit.Blocks);
        return reader.ReadAllBlocks();
    }
    catch (Exception ex)
    {
        log.LogWarning(ex, "    (could not decode commit blocks)");
        return null;
    }
}

static void LogOpPayload(SubscribeReposRepoOp op, Dictionary<string, byte[]>? blocks, bool showPayload, ILogger log)
{
    if (!showPayload)
    {
        return;
    }

    var cid = op.Cid?.ToString();
    if (string.IsNullOrEmpty(cid))
    {
        log.LogInformation("    (no payload: {Action})", op.Action);
        return;
    }

    if (blocks == null || !blocks.TryGetValue(cid, out var block))
    {
        log.LogInformation("    (no payload: block {Cid} not in commit)", cid);
        return;
    }

    try
    {
        // Note: PeterO renders embedded CID links as tagged byte strings rather
        // than the atproto {"$link": ...} JSON form; good enough for a debug tail.
        var json = CBORObject.DecodeFromBytes(block).ToJSONString();
        log.LogInformation("    {Payload}", json);
    }
    catch (Exception ex)
    {
        log.LogWarning(ex, "    (could not decode block {Cid})", cid);
    }
}
