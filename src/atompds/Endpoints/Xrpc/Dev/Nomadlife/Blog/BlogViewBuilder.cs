using System.Text.Json;
using AccountManager;
using ActorStore;
using AppBsky.Actor;
using CarpaNet;
using Config;
using DevNomadlife.Blog;

namespace atompds.Endpoints.Xrpc.Dev.Nomadlife.Blog;

public static class BlogViewBuilder
{
    public static async Task<DefsProfileViewDetailed> ResolveAuthorProfileAsync(
        AccountRepository accountRepository, ActorRepositoryProvider actorRepositoryProvider,
        ServiceConfig serviceConfig, string did, string author)
    {
        var accounts = await accountRepository.GetAccountsAsync([did]);
        accounts.TryGetValue(did, out var authorAccount);

        string? displayName = null;
        string? description = null;
        string? pronouns = null;
        string? website = null;
        string? avatarUrl = null;
        string? bannerUrl = null;
        DateTime? createdAt = authorAccount?.CreatedAt;
        DateTime? profileIndexedAt = null;

        if (actorRepositoryProvider.Exists(did))
        {
            await using var actorStore = actorRepositoryProvider.Open(did);
            var uri = ATUri.Create(did, "app.bsky.actor.profile", "self");
            var record = await actorStore.Record.GetRecordAsync(uri, null, true);
            if (record != null)
            {
                if (record.Value.TryGetProperty("displayName", out var dn) && dn.ValueKind == JsonValueKind.String)
                    displayName = dn.GetString();
                if (record.Value.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String)
                    description = desc.GetString();
                if (record.Value.TryGetProperty("pronouns", out var pro) && pro.ValueKind == JsonValueKind.String)
                    pronouns = pro.GetString();
                if (record.Value.TryGetProperty("website", out var web) && web.ValueKind == JsonValueKind.String)
                    website = web.GetString();
                if (TryGetBlobCid(record.Value, "avatar", out var avatarCid))
                    avatarUrl = $"{serviceConfig.PublicUrl}/xrpc/com.atproto.sync.getBlob?did={did}&cid={avatarCid}";
                if (TryGetBlobCid(record.Value, "banner", out var bannerCid))
                    bannerUrl = $"{serviceConfig.PublicUrl}/xrpc/com.atproto.sync.getBlob?did={did}&cid={bannerCid}";
                profileIndexedAt = record.IndexedAt;
            }
        }

        return new DefsProfileViewDetailed
        {
            Did = new ATDid(did),
            Handle = new ATHandle(authorAccount?.Handle ?? (author.StartsWith("did:") ? did : author)),
            DisplayName = displayName,
            Description = description,
            Pronouns = pronouns,
            Website = website,
            Avatar = avatarUrl,
            Banner = bannerUrl,
            CreatedAt = createdAt,
            IndexedAt = profileIndexedAt ?? createdAt,
            Viewer = null,
            Labels = null,
            PostsCount = null,
            FollowersCount = null,
            FollowsCount = null,
            PinnedPost = null,
            JoinedViaStarterPack = null,
            Debug = null,
            Verification = null,
            Associated = null
        };
    }

    private static bool TryGetBlobCid(JsonElement record, string propertyName, out string cid)
    {
        cid = "";
        if (!record.TryGetProperty(propertyName, out var blobElem) || blobElem.ValueKind != JsonValueKind.Object)
            return false;
        if (!blobElem.TryGetProperty("ref", out var refElem) || refElem.ValueKind != JsonValueKind.Object)
            return false;
        if (refElem.TryGetProperty("$link", out var linkElem) && linkElem.ValueKind == JsonValueKind.String)
        {
            cid = linkElem.GetString()!;
            return true;
        }
        if (refElem.TryGetProperty("link", out var linkElem2) && linkElem2.ValueKind == JsonValueKind.String)
        {
            cid = linkElem2.GetString()!;
            return true;
        }
        return false;
    }

    public static DefsPostView BuildPostView(
        ActorStore.Repo.RecordRepository.RecordForCollection record, string did, ServiceConfig serviceConfig,
        DefsProfileViewDetailed profile)
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
        };
    }

    public static DefsImageView? ResolveImage(JsonElement record, string did, ServiceConfig serviceConfig)
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

    public static List<string>? ParseTags(JsonElement record)
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

    public static ATUri? ParseCategory(JsonElement record)
    {
        if (!record.TryGetProperty("category", out var cat) || cat.ValueKind != JsonValueKind.String)
            return null;

        var catStr = cat.GetString();
        if (string.IsNullOrEmpty(catStr))
            return null;

        return new ATUri(catStr);
    }

    public static bool TryExtractCid(JsonElement blobObj, out string cid)
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