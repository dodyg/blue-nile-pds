using System.Text.Json;
using AccountManager;
using AccountManager.Db;
using ActorStore;
using AppBsky.Actor;
using Config;
using CarpaNet;
using CommonWeb;
using Xrpc;

namespace atompds.Endpoints.Xrpc.App.Bsky.Actor;

public static class GetProfileEndpoints
{
    public static RouteGroupBuilder MapGetProfileEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("app.bsky.actor.getProfile", HandleAsync);
        return group;
    }

    private static async Task<IResult> HandleAsync(
        string actor,
        HttpContext context,
        AccountRepository accountRepository,
        ActorRepositoryProvider actorRepositoryProvider,
        ServiceConfig serviceConfig,
        ILogger<Program> logger)
    {
        var account = await accountRepository.GetAccountAsync(actor, new AvailabilityFlags(true, true));
        if (account == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Profile not found."));

        JsonElement? profileRecord = null;
        DateTime? profileIndexedAt = null;
        if (actorRepositoryProvider.Exists(account.Did))
        {
            await using var actorStore = actorRepositoryProvider.Open(account.Did);
            var uri = ATUri.Create(account.Did, "app.bsky.actor.profile", "self");
            var record = await actorStore.Record.GetRecordAsync(uri, null, true);
            if (record != null)
            {
                profileRecord = record.Value;
                profileIndexedAt = record.IndexedAt;
            }
        }

        string? displayName = null;
        string? description = null;
        string? pronouns = null;
        string? website = null;
        string? avatar = null;
        string? banner = null;
        if (profileRecord.HasValue)
        {
            if (profileRecord.Value.TryGetProperty("displayName", out var dn) && dn.ValueKind == JsonValueKind.String)
                displayName = dn.GetString();
            if (profileRecord.Value.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String)
                description = desc.GetString();
            if (profileRecord.Value.TryGetProperty("pronouns", out var pro) && pro.ValueKind == JsonValueKind.String)
                pronouns = pro.GetString();
            if (profileRecord.Value.TryGetProperty("website", out var web) && web.ValueKind == JsonValueKind.String)
                website = web.GetString();
            if (TryGetBlobCid(profileRecord.Value, "avatar", out var avatarCid))
                avatar = $"{serviceConfig.PublicUrl}/xrpc/com.atproto.sync.getBlob?did={account.Did}&cid={avatarCid}";
            if (TryGetBlobCid(profileRecord.Value, "banner", out var bannerCid))
                banner = $"{serviceConfig.PublicUrl}/xrpc/com.atproto.sync.getBlob?did={account.Did}&cid={bannerCid}";
        }

        return Results.Ok(new DefsProfileViewDetailed
        {
            Did = new ATDid(account.Did),
            Handle = new ATHandle(account.Handle ?? Constants.INVALID_HANDLE),
            DisplayName = displayName,
            Description = description,
            Pronouns = pronouns,
            Website = website,
            Avatar = avatar,
            Banner = banner,
            CreatedAt = account.CreatedAt,
            IndexedAt = profileIndexedAt ?? account.CreatedAt,
            Viewer = null,
            Labels = null,
            PostsCount = null,
            FollowersCount = null,
            FollowsCount = null
        });
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
}
