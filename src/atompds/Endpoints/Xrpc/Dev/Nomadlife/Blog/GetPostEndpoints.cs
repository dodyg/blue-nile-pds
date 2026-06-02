using System.Text.Json;
using AccountManager;
using ActorStore;
using CarpaNet;
using Config;
using AppBsky.Actor;
using DevNomadlife.Blog;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Dev.Nomadlife.Blog;

public static class GetPostEndpoints
{
    public static RouteGroupBuilder MapGetPostEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("dev.nomadlife.blog.getPost", HandleAsync);
        return group;
    }

    private static async Task<IResult> HandleAsync(
        string author,
        string slug,
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        ServiceConfig serviceConfig)
    {
        var did = await accountRepository.GetDidForActorAsync(author);
        if (did is null)
            throw new XRPCError(new InvalidRequestErrorDetail(GetPostErrors.PostNotFound, "No post found with the given slug."));

        if (!actorRepositoryProvider.Exists(did))
            throw new XRPCError(new InvalidRequestErrorDetail(GetPostErrors.PostNotFound, "No post found with the given slug."));

        await using var actorStore = actorRepositoryProvider.Open(did);

        var accounts = await accountRepository.GetAccountsAsync([did]);
        accounts.TryGetValue(did, out var authorAccount);
        var profile = new DefsProfileViewBasic
        {
            Did = new ATDid(did),
            Handle = new ATHandle(authorAccount?.Handle ?? (author.StartsWith("did:") ? did : author))
        };

        const int pageSize = 100;
        string? cursor = null;

        do
        {
            var page = await actorStore.Repo.Record.ListRecordsForCollectionAsync(
                "dev.nomadlife.blog.post", pageSize, reverse: false, cursor: cursor);

            foreach (var record in page)
            {
                if (!record.Value.TryGetProperty("slug", out var slugProp) ||
                    slugProp.ValueKind != JsonValueKind.String ||
                    slugProp.GetString() != slug)
                {
                    continue;
                }

                var val = record.Value;

                string? title = null;
                if (val.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String)
                    title = t.GetString();

                var text = val.TryGetProperty("text", out var tx) && tx.ValueKind == JsonValueKind.String
                    ? tx.GetString() ?? "" : "";

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

                return Results.Ok(new GetPostOutput
                {
                    Post = new DefsPostView
                    {
                        Uri = new ATUri(record.Uri),
                        Author = profile,
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
                    }
                });
            }

            cursor = page.Count > 0 ? new ATUri(page.Last().Uri).RecordKey : null;
        } while (cursor != null);

        throw new XRPCError(new InvalidRequestErrorDetail(GetPostErrors.PostNotFound, "No post found with the given slug."));
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
