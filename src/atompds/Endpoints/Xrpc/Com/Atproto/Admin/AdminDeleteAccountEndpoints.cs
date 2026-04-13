using AccountManager;
using atompds.Middleware;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Admin;

public static class AdminDeleteAccountEndpoints
{
    public static RouteGroupBuilder MapAdminDeleteAccountEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.admin.deleteAccount", HandleAsync).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(AdminDeleteAccountInput request, AccountRepository accountRepository)
    {
        if (string.IsNullOrWhiteSpace(request.Did))
            throw new XRPCError(new InvalidRequestErrorDetail("did is required"));

        await accountRepository.DeleteAccountAsync(request.Did);
        return Results.Ok();
    }
}

public class AdminDeleteAccountInput
{
    public string? Did { get; set; }
}
