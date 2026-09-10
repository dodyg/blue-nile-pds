using BlueNilePds.Host.Configuration;

namespace BlueNilePds.Host.Endpoints.Pending;

public static class PendingConfigEndpoints
{
    public static RouteGroupBuilder MapPendingConfigEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/config", Handle);
        return group;
    }

    private static IResult Handle(ServerEnvironment environment)
    {
        // Note: HttpJsonOptions use DefaultIgnoreCondition.WhenWritingDefault,
        // so false/null values are omitted from the response. Clients must
        // treat a missing turnstileRequired as false.
        return Results.Ok(new
        {
            approvalRequired = environment.PDS_ACCOUNT_APPROVAL_REQUIRED,
            turnstileRequired = !string.IsNullOrWhiteSpace(environment.PDS_TURNSTILE_SECRET),
            turnstileSiteKey = environment.PDS_TURNSTILE_SITE_KEY
        });
    }
}
