using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ConsoleAppFramework;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;

namespace pdsadmin;

public class AccountCommands
{
    /// <summary>
    ///     List accounts
    /// </summary>
    [Command("list")]
    public async Task ListAsync()
    {
        var output = await Program.GetXrpcAsync<ListReposResponse>(
            "com.atproto.sync.listRepos",
            [new KeyValuePair<string, string?>("limit", "100")]);

        var accountInfos = output.Repos.Count == 0
            ? new GetAccountInfosResponse()
            : await Program.GetXrpcAsync<GetAccountInfosResponse>(
                "com.atproto.admin.getAccountInfos",
                [new KeyValuePair<string, string?>("dids", string.Join(',', output.Repos.Select(repo => repo.Did)))],
                admin: true);
        var accountsByDid = accountInfos.Accounts.ToDictionary(account => account.Did, StringComparer.Ordinal);
        var outputList = new List<(string Handle, string Email, string Did)>();
        foreach (var repo in output.Repos)
        {
            if (accountsByDid.TryGetValue(repo.Did, out var accountInfo))
            {
                outputList.Add((accountInfo.Handle, accountInfo.Email ?? string.Empty, accountInfo.Did));
                continue;
            }

            Program.Logger.LogWarning("Failed to find account info for {Did}", repo.Did);
        }

        Program.Logger.LogInformation("{Handle,-20} {Email,-20} {Did,-20}", "Handle", "Email", "DID");
        foreach (var (handle, email, did) in outputList)
        {
            Program.Logger.LogInformation("{Handle,-20} {Email,-20} {Did,-20}", handle, email, did);
        }
    }

    /// <summary>
    ///     Create a new account
    /// </summary>
    /// <param name="email">-e, --email, Email address ex. alice@example.com</param>
    /// <param name="handle">-h, --handle, Handle, ex. alice.example.com</param>
    [Command("create")]
    public Task CreateAsync(string email, string handle) =>
        throw new NotSupportedException(
            "pdsadmin account create is not supported against this server because admin auth uses HTTP basic auth and the server does not expose an admin invite creation endpoint.");

    /// <summary>
    ///     Delete an account specified by DID (admin)
    /// </summary>
    /// <param name="did">-d, --did, DID, ex. did:plc:xyz123abc456</param>
    [Command("admin-delete")]
    public async Task AdminDeleteAsync(string did)
    {
        await Program.PostAdminAsync("com.atproto.admin.deleteAccount", new { did });
        Program.Logger.LogInformation("{Did} deleted (admin)", did);
    }

    /// <summary>
    ///     Get account info by DID
    /// </summary>
    /// <param name="did">-d, --did, DID, ex. did:plc:xyz123abc456</param>
    [Command("info")]
    public async Task InfoAsync(string did)
    {
        var accountInfo = await Program.GetXrpcAsync<AccountInfo>(
            "com.atproto.admin.getAccountInfo",
            [new KeyValuePair<string, string?>("did", did)],
            admin: true);

        Program.Logger.LogInformation("DID     : {Did}", accountInfo.Did);
        Program.Logger.LogInformation("Handle  : {Handle}", accountInfo.Handle);
        Program.Logger.LogInformation("Email   : {Email}", accountInfo.Email ?? "N/A");
    }

    /// <summary>
    ///     Update account handle (admin)
    /// </summary>
    /// <param name="did">-d, --did, DID, ex. did:plc:xyz123abc456</param>
    /// <param name="handle">-h, --handle, New handle</param>
    [Command("update-handle")]
    public async Task UpdateHandleAsync(string did, string handle)
    {
        await Program.PostAdminAsync("com.atproto.admin.updateAccountHandle", new { did, handle });
        Program.Logger.LogInformation("Handle updated for {Did} to {Handle}", did, handle);
    }

    /// <summary>
    ///     Update account email (admin)
    /// </summary>
    /// <param name="did">-d, --did, DID, ex. did:plc:xyz123abc456</param>
    /// <param name="email">-e, --email, New email address</param>
    [Command("update-email")]
    public async Task UpdateEmailAsync(string did, string email)
    {
        await Program.PostAdminAsync("com.atproto.admin.updateAccountEmail", new { did, email });
        Program.Logger.LogInformation("Email updated for {Did} to {Email}", did, email);
    }

    /// <summary>
    ///     Enable account invites
    /// </summary>
    /// <param name="did">-d, --did, DID, ex. did:plc:xyz123abc456</param>
    [Command("enable-invites")]
    public async Task EnableInvitesAsync(string did)
    {
        await Program.PostAdminAsync("com.atproto.admin.enableAccountInvites", new DidRequest(did));
        Program.Logger.LogInformation("Invites enabled for {Did}", did);
    }

    /// <summary>
    ///     Disable account invites
    /// </summary>
    /// <param name="did">-d, --did, DID, ex. did:plc:xyz123abc456</param>
    [Command("disable-invites")]
    public async Task DisableInvitesAsync(string did)
    {
        await Program.PostAdminAsync("com.atproto.admin.disableAccountInvites", new DidRequest(did));
        Program.Logger.LogInformation("Invites disabled for {Did}", did);
    }

    /// <summary>
    ///     Delete an account specified by DID
    /// </summary>
    /// <param name="did">-d, --did, DID, ex. did:plc:xyz123abc456</param>
    /// <param name="token">-t, --token, Account deletion token sent to the user's email</param>
    /// <param name="password">-p, --password, Current password for the account being deleted</param>
    [Command("delete")]
    public async Task DeleteAsync(string did, string token, string password)
    {
        await Program.PostXrpcAsync(
            "com.atproto.server.deleteAccount",
            new DeleteAccountRequest(did, token, password));

        Program.Logger.LogInformation("{Did} deleted", did);
    }

    /// <summary>
    ///     Takedown an account specified by DID
    /// </summary>
    /// <param name="did">-d, --did, DID, ex. did:plc:xyz123abc456</param>
    [Command("takedown")]
    public async Task TakedownAsync(string did)
    {
        await Program.PostAdminAsync(
            "com.atproto.admin.updateSubjectStatus",
            new UpdateSubjectStatusRequest(did, new TakedownRequest(true)));

        Program.Logger.LogInformation("{Did} taken down", did);
    }

    /// <summary>
    ///     Remove a takedown from an account specified by DID
    /// </summary>
    /// <param name="did">-d, --did, DID, ex. did:plc:xyz123abc456</param>
    [Command("untakedown")]
    public async Task UntakedownAsync(string did)
    {
        await Program.PostAdminAsync(
            "com.atproto.admin.updateSubjectStatus",
            new UpdateSubjectStatusRequest(did, new TakedownRequest(false)));

        Program.Logger.LogInformation("{Did} untaken down", did);
    }

    /// <summary>
    ///     Reset a password for an account specified by DID
    /// </summary>
    /// <param name="did">-d, --did, DID, ex. did:plc:xyz123abc456</param>
    [Command("reset-password")]
    public async Task ResetPasswordAsync(string did)
    {
        var passwordStr = Program.GeneratePassword();
        await Program.PostAdminAsync(
            "com.atproto.admin.updateAccountPassword",
            new UpdatePasswordRequest(did, passwordStr));

        Program.Logger.LogInformation("Password reset for {Did}", did);
        Program.Logger.LogInformation("New password: {Password}", passwordStr);
    }
}

public class RootCommands
{
    /// <summary>
    ///     Update to the latest PDS version
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    [Command("update")]
    public void Update()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    ///     Request a crawl from a relay host
    /// </summary>
    /// <param name="relayHost">-r, --relay-host, Relay host, ex. bsky.network</param>
    [Command("request-crawl")]
    public async Task RequestCrawlAsync(string relayHost)
    {
        var relayHosts = relayHost.Split(',');
        var client = new HttpClient();
        foreach (var host in relayHosts)
        {
            var lh = host.Trim();
            Program.Logger.LogInformation("Requesting crawl from {Host}", lh);
            if (!lh.StartsWith("http://") && !lh.StartsWith("https://"))
            {
                lh = $"https://{host}";
            }

            var response = await client.PostAsJsonAsync($"{lh}/xrpc/com.atproto.sync.requestCrawl", new
            {
                hostname = Program.PdsEnv.PdsHostname
            });
            if (!response.IsSuccessStatusCode)
            {
                Program.Logger.LogError("Failed to request crawl from {Host}", lh);
            }
        }

        Program.Logger.LogInformation("done");
    }

    /// <summary>
    ///     Create a new invite code
    /// </summary>
    [Command("create-invite-code")]
    public Task CreateInviteCodeAsync() =>
        throw new NotSupportedException(
            "pdsadmin create-invite-code is not supported against this server because admin auth uses HTTP basic auth and the server does not expose an admin invite creation endpoint.");
}

public record PdsEnv(
    string PdsHostname,
    string PdsAdminPassword
);

public record RepoEntry(string Did);
public sealed class ListReposResponse
{
    public IReadOnlyList<RepoEntry> Repos { get; init; } = [];
}

public record AccountInfo(
    string Did,
    string Handle,
    string? Email);

public sealed class GetAccountInfosResponse
{
    public IReadOnlyList<AccountInfo> Accounts { get; init; } = [];
}

public record DidRequest(string Did);
public record DeleteAccountRequest(string Did, string Token, string Password);
public record TakedownRequest(bool Applied);
public record UpdateSubjectStatusRequest(string Did, TakedownRequest Takedown);
public record UpdatePasswordRequest(string Did, string Password);

public class Program
{
    public static PdsEnv PdsEnv { get; set; } = null!;
    public static ILogger Logger { get; set; } = null!;
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    public static string GeneratePassword()
    {
        var rnd = RandomNumberGenerator.Create();
        var password = new byte[30];
        rnd.GetBytes(password);
        return Convert.ToBase64String(password).Replace("=", "").Replace("+", "").Replace("/", "")[..24];
    }

    public static Uri GetBaseUri()
    {
        var host = PdsEnv.PdsHostname.TrimEnd('/');
        if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(host, UriKind.Absolute);
        }

        return new Uri($"https://{host}", UriKind.Absolute);
    }

    public static HttpClient CreateHttpClient(bool admin = false)
    {
        var client = new HttpClient
        {
            BaseAddress = GetBaseUri()
        };

        if (admin)
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"admin:{PdsEnv.PdsAdminPassword}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        return client;
    }

    public static async Task<T> GetXrpcAsync<T>(
        string nsid,
        IEnumerable<KeyValuePair<string, string?>>? query = null,
        bool admin = false)
    {
        using var client = CreateHttpClient(admin);
        using var response = await client.GetAsync(BuildXrpcPath(nsid, query));
        return await ReadJsonResponseAsync<T>(response, nsid);
    }

    public static async Task PostXrpcAsync(string nsid, object payload, bool admin = false)
    {
        using var client = CreateHttpClient(admin);
        using var response = await client.PostAsJsonAsync(BuildXrpcPath(nsid), payload, JsonOptions);
        await EnsureSuccessAsync(response, nsid);
    }

    public static Task PostAdminAsync(string nsid, object payload)
    {
        return PostXrpcAsync(nsid, payload, admin: true);
    }

    private static string BuildXrpcPath(string nsid, IEnumerable<KeyValuePair<string, string?>>? query = null)
    {
        var path = $"/xrpc/{nsid}";
        if (query == null)
        {
            return path;
        }

        var encoded = query
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}")
            .ToArray();

        return encoded.Length == 0 ? path : $"{path}?{string.Join("&", encoded)}";
    }

    private static async Task<T> ReadJsonResponseAsync<T>(HttpResponseMessage response, string nsid)
    {
        await EnsureSuccessAsync(response, nsid);
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        return result ?? throw new InvalidOperationException($"Empty JSON response for {nsid}.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string nsid)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Request failed: {response.StatusCode} {nsid} - {body}");
        }
    }

    public static async Task Main(string[] args)
    {
        var debugLog = new DebugLoggerProvider();
        Logger = debugLog.CreateLogger("pdsadmin");

        var pdsEnvContent = await File.ReadAllTextAsync("pdsenv.json");
        var pdsEnv = JsonSerializer.Deserialize<PdsEnv>(pdsEnvContent) ??
                     throw new Exception("Failed to read pdsenv.json");
        PdsEnv = pdsEnv;

        var app = ConsoleApp.Create();
        app.Add<RootCommands>();
        app.Add<AccountCommands>("account");
        try
        {
            await app.RunAsync(args);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or
            JsonException or
            InvalidOperationException or
            NotSupportedException)
        {
            Logger.LogError(ex, "{Message}", ex.Message);
            Environment.ExitCode = 1;
        }
    }
}
