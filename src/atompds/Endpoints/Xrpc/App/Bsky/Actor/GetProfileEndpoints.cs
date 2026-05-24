using System.Text.Json;
using AccountManager;
using AccountManager.Db;
using ActorStore;
using AppBsky.Actor;
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
        }

        return Results.Ok(new DefsProfileViewDetailed
        {
            Did = new ATDid(account.Did),
            Handle = new ATHandle(account.Handle ?? Constants.INVALID_HANDLE),
            DisplayName = displayName,
            Description = description,
            Pronouns = pronouns,
            Website = website,
            Avatar = null,
            Banner = null,
            CreatedAt = account.CreatedAt,
            IndexedAt = profileIndexedAt ?? account.CreatedAt,
            Viewer = null,
            Labels = null,
            PostsCount = null,
            FollowersCount = null,
            FollowsCount = null
        });
    }
}
