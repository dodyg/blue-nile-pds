using atompds.Services;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class ReserveSigningKeyEndpoints
{
    public static RouteGroupBuilder MapReserveSigningKeyEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.server.reserveSigningKey", HandleAsync).RequireRateLimiting("auth-sensitive");
        return group;
    }

    private static async Task<IResult> HandleAsync(
        ReserveSigningKeyInput? request,
        ReservedSigningKeyStore reservedSigningKeyStore)
    {
        var signingKey = await reservedSigningKeyStore.ReserveAsync(request?.Did);
        return Results.Ok(new { signingKey });
    }
}

public class ReserveSigningKeyInput
{
    public string? Did { get; set; }
}
