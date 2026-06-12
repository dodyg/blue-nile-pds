using System.Text.Json;
using AccountManager;
using ActorStore;
using CarpaNet;
using Config;
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

        var profile = await BlogViewBuilder.ResolveAuthorProfileAsync(accountRepository, actorRepositoryProvider, serviceConfig, did, author);

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

                return Results.Ok(new GetPostOutput
                {
                    Post = BlogViewBuilder.BuildPostView(record, did, serviceConfig, profile)
                });
            }

            cursor = page.Count > 0 ? new ATUri(page.Last().Uri).RecordKey : null;
        } while (cursor != null);

        throw new XRPCError(new InvalidRequestErrorDetail(GetPostErrors.PostNotFound, "No post found with the given slug."));
    }

}