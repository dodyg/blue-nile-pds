using atompds.Middleware;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Temp;

public static class CheckSignupQueueEndpoints
{
    public static RouteGroupBuilder MapCheckSignupQueueEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.temp.checkSignupQueue", HandleAsync);
        return group;
    }

    private static async Task<IResult> HandleAsync(HttpContext context, AuthVerifier authVerifier)
    {
        await authVerifier.ValidateAccessTokenAsync(context,
        [
            AuthVerifier.ScopeMap[AuthVerifier.AuthScope.SignupQueued]
        ]);

        return Results.Ok(new { activated = true });
    }
}
