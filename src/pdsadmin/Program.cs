using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using CarpaNet;
using ConsoleAppFramework;
using CommonWeb.Generated;
using ComAtproto.Admin;
using ComAtproto.Server;
using ComAtproto.Sync;
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
        var client = await Program.CreateAuthenticatedClientAsync();
        var output = await client.ComAtprotoSyncListReposAsync(new ListReposParameters { Limit = 100 });

        var outputList = new List<(string Handle, string Email, string Did)>();
        foreach (var repo in output.Repos)
        {
            try
            {
                var accountInfo = await client.ComAtprotoAdminGetAccountInfoAsync(new GetAccountInfoParameters { Did = repo.Did });
                outputList.Add((accountInfo.Handle.Value, accountInfo.Email ?? string.Empty, accountInfo.Did.Value));
            }
            catch (ATProtoException ex)
            {
                Program.Logger.LogError(ex, "Failed to get account info for {Did} {ErrorCode} {Message}", repo.Did, ex.ErrorCode, ex.Message);
            }
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
    public async Task CreateAsync(string email, string handle)
    {
        var client = await Program.CreateAuthenticatedClientAsync();
        var atHandle = new ATHandle(handle);
        var rnd = RandomNumberGenerator.Create();
        var password = new byte[30];
        rnd.GetBytes(password);
        var passwordStr = Convert.ToBase64String(password).Replace("=", "").Replace("+", "").Replace("/", "")[..24];

        var inviteCode = await Program.CreateInviteCodeAsync(1);
        var result = await client.ComAtprotoServerCreateAccountAsync(new CreateAccountInput
        {
            Handle = atHandle,
            Email = email,
            Password = passwordStr,
            InviteCode = inviteCode.Code
        });

        Program.Logger.LogInformation("Account created successfully!");
        Program.Logger.LogInformation("Handle   : {Handle}", handle);
        Program.Logger.LogInformation("DID      : {Did}", result.Did);
        Program.Logger.LogInformation("Password : {Password}", passwordStr);
        Program.Logger.LogInformation("Save this password, it will not be displayed again.");
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
        var client = await Program.CreateAuthenticatedClientAsync();
        await client.ComAtprotoServerDeleteAccountAsync(new DeleteAccountInput
        {
            Did = new ATDid(did),
            Token = token,
            Password = password
        });

        Program.Logger.LogInformation("{Did} deleted", did);
    }

    /// <summary>
    ///     Takedown an account specified by DID
    /// </summary>
    /// <param name="did">-d, --did, DID, ex. did:plc:xyz123abc456</param>
    [Command("takedown")]
    public async Task TakedownAsync(string did)
    {
        var client = await Program.CreateAuthenticatedClientAsync();
        var atDid = new ATDid(did);
        var takedownRef = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await client.ComAtprotoAdminUpdateSubjectStatusAsync(new UpdateSubjectStatusInput
        {
            Subject = new DefsRepoRef { Did = atDid },
            Takedown = new DefsStatusAttr { Applied = true, Ref = takedownRef.ToString() }
        });

        Program.Logger.LogInformation("{Did} taken down", did);
    }

    /// <summary>
    ///     Remove a takedown from an account specified by DID
    /// </summary>
    /// <param name="did">-d, --did, DID, ex. did:plc:xyz123abc456</param>
    [Command("untakedown")]
    public async Task UntakedownAsync(string did)
    {
        var client = await Program.CreateAuthenticatedClientAsync();
        var atDid = new ATDid(did);
        await client.ComAtprotoAdminUpdateSubjectStatusAsync(new UpdateSubjectStatusInput
        {
            Subject = new DefsRepoRef { Did = atDid },
            Takedown = new DefsStatusAttr { Applied = false }
        });

        Program.Logger.LogInformation("{Did} untaken down", did);
    }

    /// <summary>
    ///     Reset a password for an account specified by DID
    /// </summary>
    /// <param name="did">-d, --did, DID, ex. did:plc:xyz123abc456</param>
    [Command("reset-password")]
    public async Task ResetPasswordAsync(string did)
    {
        var client = await Program.CreateAuthenticatedClientAsync();
        var atDid = new ATDid(did);
        var rnd = RandomNumberGenerator.Create();
        var password = new byte[30];
        rnd.GetBytes(password);
        var passwordStr = Convert.ToBase64String(password).Replace("=", "").Replace("+", "").Replace("/", "")[..24];
        await client.ComAtprotoAdminUpdateAccountPasswordAsync(new UpdateAccountPasswordInput
        {
            Did = atDid,
            Password = passwordStr
        });

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
    public async Task CreateInviteCodeAsync()
    {
        var result = await Program.CreateInviteCodeAsync(1);
        Program.Logger.LogInformation("Invite code: {Code}", result.Code);
    }
}

public record PdsEnv(
    string PdsHostname,
    string PdsAdminPassword
);

public class Program
{
    public static PdsEnv PdsEnv { get; set; } = null!;
    public static ILogger Logger { get; set; } = null!;

    public static async Task<CreateInviteCodeOutput> CreateInviteCodeAsync(int useCount)
    {
        var client = await CreateAuthenticatedClientAsync();
        return await client.ComAtprotoServerCreateInviteCodeAsync(new CreateInviteCodeInput
        {
            UseCount = useCount
        });
    }

    public static async Task<ATProtoClient> CreateAuthenticatedClientAsync()
    {
        return await ATProtoClientFactory.CreateWithSessionAsync(
            "admin",
            PdsEnv.PdsAdminPassword,
            new Uri($"https://{PdsEnv.PdsHostname}"));
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
        catch (ATProtoException ex)
        {
            Logger.LogError(ex, "ATProto request failed {StatusCode} {ErrorCode} {Message}", ex.StatusCode, ex.ErrorCode, ex.Message);
            Environment.ExitCode = 1;
        }
    }
}
