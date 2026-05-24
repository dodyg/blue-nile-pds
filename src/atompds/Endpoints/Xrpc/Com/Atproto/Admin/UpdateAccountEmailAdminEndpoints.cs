using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Admin;

public static class UpdateAccountEmailAdminEndpoints
{
    public static RouteGroupBuilder MapUpdateAccountEmailAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.admin.updateAccountEmail", HandleAsync).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(AdminUpdateEmailInput request, AccountRepository accountRepository)
    {
        if (string.IsNullOrWhiteSpace(request.Did) || string.IsNullOrWhiteSpace(request.Email))
            throw new XRPCError(new InvalidRequestErrorDetail("did and email are required"));

        await accountRepository.UpdateEmailAsync(request.Did, request.Email);
        return Results.Ok();
    }
}

public class AdminUpdateEmailInput
{
    public string? Did { get; set; }
    public string? Email { get; set; }
}
