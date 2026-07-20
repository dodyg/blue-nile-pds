using AccountManager;
using atompds.Middleware;
using CarpaNet;
using ComAtproto.Admin;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Admin;

public static class AdminDeleteAccountEndpoints
{
    public static RouteGroupBuilder MapAdminDeleteAccountEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.admin.deleteAccount", HandleAsync).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(DeleteAccountInput request, AccountRepository accountRepository, ILoggerFactory loggerFactory)
    {
        await accountRepository.DeleteAccountAsync(request.Did);
        var logger = loggerFactory.CreateLogger("AdminDeleteAccountEndpoints");
        logger.LogWarning("Admin account deleted: {Did}", (string)request.Did);
        return Results.Ok(new { });
    }
}
