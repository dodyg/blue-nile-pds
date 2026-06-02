using System.Text.Json;
using AccountManager;
using ActorStore;
using CarpaNet;
using Config;
using AppBsky.Actor;
using DevNomadlife.Blog;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Dev.Nomadlife.Blog;

public static class GetAuthorPostsEndpoints
{
    public static RouteGroupBuilder MapGetAuthorPostsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("dev.nomadlife.blog.getAuthorPosts", HandleAsync);
        return group;
    }

    private static async Task<IResult> HandleAsync(
        string author,
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        ServiceConfig serviceConfig,
        int limit = 20,
        string? cursor = null)
    {
        var did = await accountRepository.GetDidForActorAsync(author);
        if (did is null)
            throw new XRPCError(new InvalidRequestErrorDetail(
                GetAuthorPostsErrors.AuthorNotFound, "No repository found for the given actor."));

        if (!actorRepositoryProvider.Exists(did))
            throw new XRPCError(new InvalidRequestErrorDetail(
                GetAuthorPostsErrors.AuthorNotFound, "No repository found for the given actor."));

        await using var actorStore = actorRepositoryProvider.Open(did);

        var accounts = await accountRepository.GetAccountsAsync([did]);
        accounts.TryGetValue(did, out var authorAccount);
        var profile = new DefsProfileViewBasic
        {
            Did = new ATDid(did),
            Handle = new ATHandle(authorAccount?.Handle ?? (author.StartsWith("did:") ? did : author))
        };

        var page = await actorStore.Repo.Record.ListRecordsForCollectionAsync(
            "dev.nomadlife.blog.post", limit, reverse: false, cursor: cursor);

        var posts = page.Select(record => BuildPostView(record, did, serviceConfig, profile)).ToList();

        var last = page.Count > 0 ? page[^1] : null;
        var nextCursor = last is not null ? new ATUri(last.Uri).RecordKey : null;

        return Results.Ok(new GetAuthorPostsOutput
        {
            Cursor = nextCursor,
            Posts = posts
        });
    }

    private static DefsPostView BuildPostView(
        ActorStore.Repo.RecordRepository.RecordForCollection record, string did, ServiceConfig serviceConfig,
        DefsProfileViewBasic author)
    {
        var val = record.Value;

        string? title = null;
        if (val.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String)
            title = t.GetString();

        var text = val.TryGetProperty("text", out var tx) && tx.ValueKind == JsonValueKind.String
            ? tx.GetString() ?? "" : "";

        var rkey = new ATUri(record.Uri).RecordKey;
        var slug = val.TryGetProperty("slug", out var s) && s.ValueKind == JsonValueKind.String
            ? s.GetString() ?? rkey ?? ""
            : rkey ?? "";

        string? description = null;
        if (val.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String)
            description = d.GetString();

        var createdAt = val.TryGetProperty("createdAt", out var ca) && ca.ValueKind == JsonValueKind.String
            ? DateTimeOffset.Parse(ca.GetString()!) : DateTimeOffset.MinValue;

        var image = ResolveImage(val, did, serviceConfig);
        var tags = ParseTags(val);
        var category = ParseCategory(val);

        DateTimeOffset? publishedAt = null;
        if (val.TryGetProperty("publishedAt", out var pa) && pa.ValueKind == JsonValueKind.String)
            publishedAt = DateTimeOffset.Parse(pa.GetString()!);

        DateTimeOffset? updatedAt = null;
        if (val.TryGetProperty("updatedAt", out var ua) && ua.ValueKind == JsonValueKind.String)
            updatedAt = DateTimeOffset.Parse(ua.GetString()!);

        return new DefsPostView
        {
            Uri = new ATUri(record.Uri),
            Author = author,
            Title = title,
            Slug = slug,
            Text = text,
            Description = description,
            Image = image,
            Tags = tags,
            Category = category,
            PublishedAt = publishedAt,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    private static DefsImageView? ResolveImage(JsonElement record, string did, ServiceConfig serviceConfig)
    {
        if (!record.TryGetProperty("image", out var img) || img.ValueKind != JsonValueKind.Object)
            return null;

        if (!img.TryGetProperty("blob", out var blob) || blob.ValueKind != JsonValueKind.Object)
            return null;

        if (!TryExtractCid(blob, out var cid))
            return null;

        var url = $"{serviceConfig.PublicUrl}/xrpc/com.atproto.sync.getBlob?did={did}&cid={cid}";

        string? alt = null;
        if (img.TryGetProperty("alt", out var a) && a.ValueKind == JsonValueKind.String)
            alt = a.GetString();

        return new DefsImageView { Url = url, Alt = alt };
    }

    private static List<string>? ParseTags(JsonElement record)
    {
        if (!record.TryGetProperty("tags", out var tagsElem) || tagsElem.ValueKind != JsonValueKind.Array)
            return null;

        var tags = new List<string>();
        foreach (var item in tagsElem.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
                tags.Add(item.GetString()!);
        }
        return tags;
    }

    private static ATUri? ParseCategory(JsonElement record)
    {
        if (!record.TryGetProperty("category", out var cat) || cat.ValueKind != JsonValueKind.String)
            return null;

        var catStr = cat.GetString();
        if (string.IsNullOrEmpty(catStr))
            return null;

        return new ATUri(catStr);
    }

    private static bool TryExtractCid(JsonElement blobObj, out string cid)
    {
        cid = "";
        if (!blobObj.TryGetProperty("ref", out var refElem) || refElem.ValueKind != JsonValueKind.Object)
            return false;

        if (refElem.TryGetProperty("$link", out var link) && link.ValueKind == JsonValueKind.String)
        {
            cid = link.GetString()!;
            return true;
        }

        if (refElem.TryGetProperty("link", out var link2) && link2.ValueKind == JsonValueKind.String)
        {
            cid = link2.GetString()!;
            return true;
        }

        return false;
    }
}