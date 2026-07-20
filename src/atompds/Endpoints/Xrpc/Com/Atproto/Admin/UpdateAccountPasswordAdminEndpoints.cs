using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using ComAtproto.Admin;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Admin;

public static class UpdateAccountPasswordAdminEndpoints
{
    public static RouteGroupBuilder MapUpdateAccountPasswordAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.admin.updateAccountPassword", HandleAsync).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(UpdateAccountPasswordInput request, AccountRepository accountRepository)
    {
        var did = (string)request.Did;
        if (string.IsNullOrWhiteSpace(did) || string.IsNullOrWhiteSpace(request.Password))
            throw new XRPCError(new InvalidRequestErrorDetail("did and password are required"));

        await accountRepository.UpdatePasswordAsync(did, request.Password);
        return Results.Ok(new { });
    }
}
