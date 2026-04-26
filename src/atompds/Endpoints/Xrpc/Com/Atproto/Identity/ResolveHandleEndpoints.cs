using System.Text.Json;
using AccountManager;
using CarpaNet;
using ComAtproto.Identity;
using Config;
using Handle;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Identity;

public static class ResolveHandleEndpoints
{
    public static RouteGroupBuilder MapResolveHandleEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.identity.resolveHandle", HandleAsync);
        return group;
    }

    private static async Task<IResult> HandleAsync(
        string handle,
        AccountRepository accountRepository,
        HandleManager handleManager,
        IdentityConfig identityConfig,
        HttpClient client,
        IBskyAppViewConfig appViewConfig,
        ILogger<Program> logger)
    {
        logger.LogInformation("Resolving handle {Handle}", handle);
        handle = handleManager.NormalizeAndEnsureValidHandle(handle);

        string? did = null;
        var user = await accountRepository.GetAccountAsync(handle);
        if (user != null)
        {
            did = user.Did;
        }

        if (did == null)
        {
            did = await TryResolveFromAppViewAsync(handle, appViewConfig, client);
        }

        if (did == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Unable to resolve handle"));

        return Results.Ok(new ResolveHandleOutput
        {
            Did = new ATDid(did)
        });
    }

    private static async Task<string?> TryResolveFromAppViewAsync(string handle, IBskyAppViewConfig appViewConfig, HttpClient client)
    {
        if (appViewConfig is BskyAppViewConfig config)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{config.Url}/xrpc/com.atproto.identity.resolveHandle?handle={handle}");
            var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var jobj = JsonDocument.Parse(content).RootElement;
                if (jobj.TryGetProperty("did", out var didProp))
                {
                    return didProp.GetString();
                }
            }
        }
        return null;
    }
}
