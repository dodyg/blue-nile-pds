using System.Text.Json;
using ActorStore;
using atompds.Middleware;
using AppBsky.Actor;
using Xrpc;

namespace atompds.Endpoints.Xrpc.App.Bsky.Actor;

public static class GetPreferencesEndpoints
{
    public static RouteGroupBuilder MapGetPreferencesEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("app.bsky.actor.getPreferences", HandleAsync)
            .WithMetadata(new AccessStandardAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        ActorRepositoryProvider actorRepositoryProvider)
    {
        var auth = context.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        if (!actorRepositoryProvider.Exists(did))
        {
            return Results.Ok(new GetPreferencesOutput { Preferences = [] });
        }

        await using var actorStore = actorRepositoryProvider.Open(did);
        var rawPrefs = await actorStore.GetPreferencesAsync("app.bsky");

        var prefs = new List<IDefsPreferences>(rawPrefs.Count);
        foreach (var je in rawPrefs)
        {
            var typed = JsonSerializer.Deserialize<IDefsPreferences>(je.GetRawText());
            if (typed != null)
                prefs.Add(typed);
        }

        return Results.Ok(new GetPreferencesOutput { Preferences = prefs });
    }
}
