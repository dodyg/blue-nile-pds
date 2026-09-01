using atompds.Config;

namespace atompds.Endpoints.Pending;

public static class PendingConfigEndpoints
{
    public static RouteGroupBuilder MapPendingConfigEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/config", Handle);
        return group;
    }

    private static IResult Handle(ServerEnvironment environment)
    {
        return Results.Ok(new
        {
            approvalRequired = environment.PDS_ACCOUNT_APPROVAL_REQUIRED
        });
    }
}
