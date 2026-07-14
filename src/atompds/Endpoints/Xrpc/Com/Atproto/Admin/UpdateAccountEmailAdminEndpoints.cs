using AccountManager;
using atompds.Middleware;
using ComAtproto.Admin;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Admin;

public static class UpdateAccountEmailAdminEndpoints
{
    public static RouteGroupBuilder MapUpdateAccountEmailAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.admin.updateAccountEmail", HandleAsync).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(UpdateAccountEmailInput request, AccountRepository accountRepository)
    {
        var account = (string)request.Account;
        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(request.Email))
            throw new XRPCError(new InvalidRequestErrorDetail("account and email are required"));

        await accountRepository.UpdateEmailAsync(account, request.Email);
        return Results.Ok(new { });
    }
}
