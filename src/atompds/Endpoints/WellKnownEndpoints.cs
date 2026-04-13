using System.Text.Json.Serialization;
using AccountManager;
using atompds.Services;
using Config;

namespace atompds.Endpoints;

public static class WellKnownEndpoints
{
    public static WebApplication MapWellKnownEndpoints(this WebApplication app)
    {
        app.MapGet(".well-known/oauth-protected-resource", HandleOAuthProtectedResource);
        app.MapGet(".well-known/oauth-authorization-server", HandleOAuthAuthorizationServer);
        app.MapGet(".well-known/atproto-did", HandleAtprotoDidAsync);
        return app;
    }

    private static IResult HandleOAuthProtectedResource(
        ServiceConfig serviceConfig,
        EntrywayRelayService entrywayRelayService)
    {
        var authServer = entrywayRelayService.IsConfigured
            ? entrywayRelayService.EntrywayUrl!
            : serviceConfig.PublicUrl;

        return Results.Ok(new WellProtectedResourceResponse(
            serviceConfig.PublicUrl,
            [authServer],
            [],
            ["header"],
            "https://atproto.com"));
    }

    private static IResult HandleOAuthAuthorizationServer(
        ServiceConfig serviceConfig,
        EntrywayRelayService entrywayRelayService)
    {
        if (entrywayRelayService.IsConfigured)
        {
            return Results.Redirect(entrywayRelayService.BuildAbsoluteUrl("/.well-known/oauth-authorization-server"));
        }

        var baseUrl = serviceConfig.PublicUrl;
        return Results.Ok(new
        {
            issuer = baseUrl,
            scopes_supported = new[] { "atproto", "transition:generic" },
            scopes_documentation = "https://atproto.com/specs/oauth",
            response_types_supported = new[] { "code" },
            grant_types_supported = new[] { "authorization_code", "refresh_token" },
            code_challenge_methods_supported = new[] { "S256" },
            authorization_endpoint = $"{baseUrl}/oauth/authorize",
            token_endpoint = $"{baseUrl}/oauth/token",
            token_endpoint_auth_methods_supported = new[] { "none" },
            dpop_signing_alg_values_supported = new[] { "ES256" },
            client_id_metadata_document = "https://atproto.com/specs/oauth#client-id-metadata-document"
        });
    }

    private static async Task<IResult> HandleAtprotoDidAsync(
        HttpContext context,
        AccountRepository accountRepository)
    {
        var host = context.Request.Host.Host;
        var acc = await accountRepository.GetAccountAsync(host);
        if (acc?.Handle == null)
        {
            return Results.NotFound("no user by that handle exists on this PDS");
        }

        return Results.Content(acc.Did);
    }

    public record WellProtectedResourceResponse(
        [property: JsonPropertyName("resource")]
        string Resource,
        [property: JsonPropertyName("authorization_servers")]
        string[] AuthorizationServers,
        [property: JsonPropertyName("scopes_supported")]
        string[] ScopesSupported,
        [property: JsonPropertyName("bearer_methods_supported")]
        string[] BearerMethodsSupported,
        [property: JsonPropertyName("resource_documentation")]
        string ResourceDocumentation);
}
