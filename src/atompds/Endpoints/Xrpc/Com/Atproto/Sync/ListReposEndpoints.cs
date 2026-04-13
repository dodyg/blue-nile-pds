using AccountManager.Db;
using atompds.Utils;
using Microsoft.EntityFrameworkCore;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Sync;

public static class ListReposEndpoints
{
    public static RouteGroupBuilder MapListReposEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.sync.listRepos", HandleAsync);
        return group;
    }

    private static async Task<IResult> HandleAsync(
        AccountManagerDb accountManagerDb,
        int limit = 500,
        string? cursor = null)
    {
        var unpackedCursor = CursorUtils.Unpack(
            cursor,
            (timeStr) =>
            {
                var valid = DateTimeOffset.TryParse(timeStr, out var dto);
                if (!valid)
                    throw new XRPCError(new InvalidRequestErrorDetail($"Malformed cursor: invalid time {timeStr}"));
                return dto.UtcDateTime;
            },
            (did) => did);

        var qb = accountManagerDb.Actors
            .Join(accountManagerDb.RepoRoots,
                actor => actor.Did,
                repoRoot => repoRoot.Did,
                (actor, repoRoot) => new
                {
                    Did = actor.Did,
                    CreatedAt = actor.CreatedAt,
                    TakedownRef = actor.TakedownRef,
                    DeactivatedAt = actor.DeactivatedAt,
                    Head = repoRoot.Cid,
                    Rev = repoRoot.Rev
                });

        qb = qb.OrderBy(x => x.CreatedAt).ThenBy(x => x.Did);

        if (unpackedCursor is not null)
        {
            var (timeCursor, didCursor) = unpackedCursor.Value;
            qb = qb.Where(x => x.CreatedAt > timeCursor || (x.CreatedAt == timeCursor && x.Did.CompareTo(didCursor) > 0));
        }

        qb = qb.Take(limit);

        var result = await qb.ToListAsync();

        var repos = result.Select(r =>
        {
            var actorAccount = new ActorAccount(r.Did, null, r.CreatedAt, r.TakedownRef, r.DeactivatedAt, null, null, null, null);
            var (active, status) = AccountStore.FormatAccountStatus(actorAccount);
            return new { r.Did, r.Head, r.Rev, active, status = status.ToString() };
        }).ToArray();

        var last = result.LastOrDefault();
        string? nextCursor = null;
        if (last is not null)
        {
            nextCursor = CursorUtils.Pack<DateTime, string>(
                (last.CreatedAt, last.Did),
                (dt) => { dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc); return dt.ToString("O"); },
                (did) => did);
        }

        return Results.Ok(new { repos, cursor = nextCursor });
    }
}
