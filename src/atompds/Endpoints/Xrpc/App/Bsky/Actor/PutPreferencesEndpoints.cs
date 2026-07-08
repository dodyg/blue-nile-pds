using System.Text.Json;
using ActorStore;
using atompds.Middleware;
using AppBsky.Actor;
using Xrpc;

namespace atompds.Endpoints.Xrpc.App.Bsky.Actor;

public static class PutPreferencesEndpoints
{
    public static RouteGroupBuilder MapPutPreferencesEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("app.bsky.actor.putPreferences", HandleAsync)
            .WithMetadata(new AccessStandardAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        PutPreferencesInput input,
        ActorRepositoryProvider actorRepositoryProvider)
    {
        var auth = context.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        if (!actorRepositoryProvider.Exists(did))
        {
            return Results.Ok();
        }

        await using var actorStore = actorRepositoryProvider.Open(did);

        var elements = new List<JsonElement>(input.Preferences.Count);
        foreach (var pref in input.Preferences)
        {
            elements.Add(JsonSerializer.SerializeToElement(pref));
        }

        await actorStore.PutPreferencesAsync("app.bsky", elements);
        return Results.Ok();
    }
}
