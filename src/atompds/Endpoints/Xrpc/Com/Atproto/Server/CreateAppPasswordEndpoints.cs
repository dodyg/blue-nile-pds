using System.Security.Cryptography;
using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Server;

public static class CreateAppPasswordEndpoints
{
    public static RouteGroupBuilder MapCreateAppPasswordEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.server.createAppPassword", HandleAsync).WithMetadata(new AccessPrivilegedAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        CreateAppPasswordInput request,
        AccountRepository accountRepository,
        AppPasswordStore appPasswordStore)
    {
        var auth = context.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new XRPCError(new InvalidRequestErrorDetail("name is required"));

        var existing = await appPasswordStore.ListAppPasswordsAsync(did);
        if (existing.Any(ap => ap.Name == request.Name))
            throw new XRPCError(new InvalidRequestErrorDetail("App password with this name already exists"));

        var password = GenerateAppPassword();
        var privileged = request.Privileged ?? false;
        await appPasswordStore.CreateAppPasswordAsync(did, request.Name, password, privileged);

        return Results.Ok(new { name = request.Name, password });
    }

    private static string GenerateAppPassword()
    {
        const string chars = "abcdefghijkmnopqrstuvwxyz23456789";
        const int length = 16;
        var segments = new List<string>();
        var current = new char[4];

        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[length];
        rng.GetBytes(bytes);

        for (var i = 0; i < length; i++)
        {
            current[i % 4] = chars[bytes[i] % chars.Length];
            if (i % 4 == 3)
                segments.Add(new string(current));
        }

        return string.Join("-", segments);
    }
}

public class CreateAppPasswordInput
{
    public string? Name { get; set; }
    public bool? Privileged { get; set; }
}
