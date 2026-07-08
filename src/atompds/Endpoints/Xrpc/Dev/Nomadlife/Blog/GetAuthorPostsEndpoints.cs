using AccountManager;
using ActorStore;
using CarpaNet;
using Config;
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

        var profile = await BlogViewBuilder.ResolveAuthorProfileAsync(accountRepository, actorRepositoryProvider, serviceConfig, did, author);

        var page = await actorStore.Repo.Record.ListRecordsForCollectionAsync(
            "dev.nomadlife.blog.post", limit, reverse: true, cursor: cursor);

        var posts = page.Select(record => BlogViewBuilder.BuildPostView(record, did, serviceConfig, profile))
            .OrderByDescending(p => p.CreatedAt)
            .ToList();

        var last = page.Count > 0 ? page[^1] : null;
        var nextCursor = last is not null ? new ATUri(last.Uri).RecordKey : null;

        return Results.Ok(new GetAuthorPostsOutput
        {
            Cursor = nextCursor,
            Posts = posts
        });
    }
}