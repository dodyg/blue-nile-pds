using atompds.Middleware;
using atompds.Services;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class GetServiceAuthEndpoints
{
    private static readonly HashSet<string> PrivilegedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "com.atproto.server.createAccount"
    };

    private static readonly HashSet<string> ProtectedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "com.atproto.admin.sendEmail",
        "com.atproto.identity.requestPlcOperationSignature",
        "com.atproto.identity.signPlcOperation",
        "com.atproto.identity.updateHandle",
        "com.atproto.server.activateAccount",
        "com.atproto.server.confirmEmail",
        "com.atproto.server.createAppPassword",
        "com.atproto.server.deactivateAccount",
        "com.atproto.server.getAccountInviteCodes",
        "com.atproto.server.getSession",
        "com.atproto.server.listAppPasswords",
        "com.atproto.server.requestAccountDelete",
        "com.atproto.server.requestEmailConfirmation",
        "com.atproto.server.requestEmailUpdate",
        "com.atproto.server.revokeAppPassword",
        "com.atproto.server.updateEmail"
    };

    public static RouteGroupBuilder MapGetServiceAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.server.getServiceAuth", Handle).WithMetadata(new AccessStandardAttribute());
        return group;
    }

    private static IResult Handle(
        HttpContext context,
        ServiceJwtBuilder serviceJwtBuilder,
        string? aud,
        long? exp,
        string? lxm)
    {
        var auth = context.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        if (string.IsNullOrWhiteSpace(aud))
            throw new XRPCError(new InvalidRequestErrorDetail("aud is required"));

        if (!aud.StartsWith("did:", StringComparison.Ordinal))
            throw new XRPCError(new InvalidRequestErrorDetail("aud must be a DID"));

        if (exp.HasValue)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var diff = exp.Value - now;
            if (diff < 0)
                throw new XRPCError(new InvalidRequestErrorDetail("BadExpiration", "expiration is in past"));
            if (diff > 60 * 60)
                throw new XRPCError(new InvalidRequestErrorDetail("BadExpiration", "cannot request a token with an expiration more than an hour in the future"));
            if (string.IsNullOrWhiteSpace(lxm) && diff > 60)
                throw new XRPCError(new InvalidRequestErrorDetail("BadExpiration", "cannot request a method-less token with an expiration more than a minute in the future"));
        }

        if (!string.IsNullOrWhiteSpace(lxm) && ProtectedMethods.Contains(lxm))
            throw new XRPCError(new InvalidRequestErrorDetail("Bad token method"));

        if (!string.IsNullOrWhiteSpace(lxm) && PrivilegedMethods.Contains(lxm) && !auth.AccessCredentials.IsPrivileged)
            throw new XRPCError(new InvalidRequestErrorDetail($"insufficient access to request a service auth token for the following method: {lxm}"));

        var token = serviceJwtBuilder.CreateServiceJwt(did, aud, lxm, exp);

        return Results.Ok(new { token });
    }
}
