using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ConsoleAppFramework;
using FishyFlip;
using FishyFlip.Lexicon;
using FishyFlip.Lexicon.Com.Atproto.Admin;
using FishyFlip.Lexicon.Com.Atproto.Server;
using FishyFlip.Lexicon.Com.Atproto.Sync;
using FishyFlip.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;
using Org.BouncyCastle.Security;
using Secp256k1Net;

namespace pdsadmin;

public partial class AccountCommands
{
    /// <summary>
    /// List accounts
    /// </summary>
    [Command("list")]
    public async Task List()
    {
        var session = await Program.LoginAsync();
        var repos = await Program.Protocol.ListReposAsync(100);

        ListReposOutput? output = null;

        repos.Switch(
            success => output = success,
            error => Program.Logger.LogError("Failed to list repos {Error} {Message}", error.Detail?.Error,
                error.Detail?.Message));

        if (output?.Repos == null)
        {
            Program.Logger.LogError("Failed to list repos");
            return;
        }

        var outputList = new List<(string Handle, string Email, string Did)>();
        foreach (var repo in output.Repos)
        {
            var accountInfo = await Program.Protocol.GetAccountInfoAsync(repo.Did!);
            accountInfo.Switch(
                accountInfoSuccess =>
                {
                  outputList.Add((accountInfoSuccess!.Handle!.Handle, accountInfoSuccess.Email!, accountInfoSuccess.Did!.Handler));
                },
                error => Program.Logger.LogError("Failed to get account info for {Did} {Error}", repo.Did, error.Detail?.Message)
            );
        }

        Program.Logger.LogInformation("{Handle,-20} {Email,-20} {Did,-20}", "Handle", "Email", "DID");
        foreach (var (handle, email, did) in outputList)
        {
            Program.Logger.LogInformation("{Handle,-20} {Email,-20} {Did,-20}", handle, email, did);
        }
    }

    /// <summary>
    /// Create a new account
    /// </summary>
    /// <param name="email">-e, --email, Email address ex. alice@example.com</param>
    /// <param name="handle">-h, --handle, Handle, ex. alice.example.com</param>
    [Command("create")]
    public async Task Create(string email, string handle)
    {
        var session = await Program.LoginAsync();
        var atHandle = ATHandle.Create(handle) ?? throw new Exception("Failed to create handle");
        var rnd = RandomNumberGenerator.Create();
        var password = new byte[30];
        rnd.GetBytes(password);
        var passwordStr = Convert.ToBase64String(password).Replace("=", "").Replace("+", "").Replace("/", "")[..24];

        var inviteCode = await Program.CreateInviteCodeAsync(1);
        var result = await Program.Protocol.CreateAccountAsync(handle: atHandle, email: email, password: passwordStr, inviteCode: inviteCode.Code);

        result.Switch(
            success =>
            {
                if (success == null)
                {
                    Program.Logger.LogError("Failed to create account");
                    return;
                }
                Program.Logger.LogInformation("Account created successfully!");
                Program.Logger.LogInformation("Handle   : {Handle}", handle);
                Program.Logger.LogInformation("DID      : {Did}", success.Did);
                Program.Logger.LogInformation("Password : {Password}", password);
                Program.Logger.LogInformation("Save this password, it will not be displayed again.");
            },
            error => Program.Logger.LogError("Failed to create account {Error} {Message}", error.Detail?.Error, error.Detail?.Message)
        );
    }

    /// <summary>
    /// Delete an account specified by DID
    /// </summary>
    /// <param name="did">-d, --did, DID, ex. did:plc:xyz123abc456</param>
    [Command("delete")]
    public async Task Delete(string did)
    {
        var session = await Program.LoginAsync();
        var atDid = ATDid.Create(did) ?? throw new Exception("Failed to create DID");
        var result = await Program.Protocol.DeleteAccountAsync(atDid);
        result.Switch(
            success => Program.Logger.LogInformation("{Did} deleted", did),
            error => Program.Logger.LogError("Failed to delete account {Did} {Error} {Message}", did, error.Detail?.Error, error.Detail?.Message)
        );
    }

    /// <summary>
    /// Takedown an account specified by DID
    /// </summary>
    /// <param name="did">-d, --did, DID, ex. did:plc:xyz123abc456</param>
    [Command("takedown")]
    public async Task Takedown(string did)
    {
        var session = await Program.LoginAsync();
      var atDid = ATDid.Create(did) ?? throw new Exception("Failed to create DID");
      var atObject = new RepoRef(atDid);
      var takedownRef = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
      var takedownAttr = new StatusAttr(true, takedownRef.ToString());
      var result = await Program.Protocol.UpdateSubjectStatusAsync(atObject, takedown: takedownAttr);
      
      result.Switch(
          success => Program.Logger.LogInformation("{Did} taken down", did),
          error => Program.Logger.LogError("Failed to take down account {Did} {Error} {Message}", did, error.Detail?.Error, error.Detail?.Message)
      );
    }

    /// <summary>
    /// Remove a takedown from an account specified by DID
    /// </summary>
    /// <param name="did">-d, --did, DID, ex. did:plc:xyz123abc456</param>
    [Command("untakedown")]
    public async Task Untakedown(string did)
    {
        var session = await Program.LoginAsync();
        var atDid = ATDid.Create(did) ?? throw new Exception("Failed to create DID");
        var atObject = new RepoRef(atDid);
        var takedownAttr = new StatusAttr(false);
        var result = await Program.Protocol.UpdateSubjectStatusAsync(atObject, takedown: takedownAttr);
        
        result.Switch(
            success => Program.Logger.LogInformation("{Did} untaken down", did),
            error => Program.Logger.LogError("Failed to untake down account {Did} {Error} {Message}", did, error.Detail?.Error, error.Detail?.Message)
        );
    }

    /// <summary>
    /// Reset a password for an account specified by DID
    /// </summary>
    /// <param name="did">-d, --did, DID, ex. did:plc:xyz123abc456</param>
    [Command("reset-password")]
    public async Task ResetPassword(string did)
    {
        var session = await Program.LoginAsync();
        var atDid = ATDid.Create(did) ?? throw new Exception("Failed to create DID");
        var rnd = RandomNumberGenerator.Create();
        var password = new byte[30];
        rnd.GetBytes(password);
        var passwordStr = Convert.ToBase64String(password).Replace("=", "").Replace("+", "").Replace("/", "")[..24];
        var result = await Program.Protocol.UpdateAccountPasswordAsync(atDid, passwordStr);
        
        result.Switch(
            success =>
            {
                Program.Logger.LogInformation("Password reset for {Did}", did);
                Program.Logger.LogInformation("New password: {Password}", passwordStr);
            },
            error => Program.Logger.LogError("Failed to reset password for account {Did} {Error} {Message}", did, error.Detail?.Error, error.Detail?.Message)
        );
    }
}

public partial class RootCommands
{
    /// <summary>
    /// Update to the latest PDS version
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    [Command("update")]
    public void Update()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Request a crawl from a relay host
    /// </summary>
    /// <param name="relayHost">-r, --relay-host, Relay host, ex. bsky.network</param>
    [Command("request-crawl")]
    public async Task RequestCrawl(string relayHost)
    {
        var relayHosts = relayHost.Split(',');
        var client  = new HttpClient();
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
    /// Create a new invite code
    /// </summary>
    [Command("create-invite-code")]
    public async Task CreateInviteCode()
    {
        var session = await Program.LoginAsync();
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
    public static ATProtocol Protocol { get; set; } = null!;
    public static ILogger Logger { get; set; } = null!;
    public static Session Session { get; set; } = null!;

    public static async Task<Session> LoginAsync()
    {
        return await Protocol.AuthenticateWithPasswordAsync("admin", PdsEnv.PdsAdminPassword) ?? throw new Exception("Failed to login");
    }

    public static async Task<CreateInviteCodeOutput> CreateInviteCodeAsync(int useCount)
    {
        var result = await Protocol.CreateInviteCodeAsync(useCount);
        CreateInviteCodeOutput? inviteCode = null;
        result.Switch(
            success => inviteCode = success,
            error => Logger.LogError("Failed to create invite code {Error} {Message}", error.Detail?.Error, error.Detail?.Message)
        );

        return inviteCode ?? throw new Exception("Failed to create invite code");
    }

    public static async Task Main(string[] args)
    {
        var debugLog = new DebugLoggerProvider();
        Logger = debugLog.CreateLogger("pdsadmin");

        var pdsEnvContent = await File.ReadAllTextAsync("pdsenv.json");
        var pdsEnv = JsonSerializer.Deserialize<PdsEnv>(pdsEnvContent) ??
                     throw new Exception("Failed to read pdsenv.json");
        PdsEnv = pdsEnv;
        var protoBuilder = new ATProtocolBuilder()
            .WithInstanceUrl(new Uri($"https://{PdsEnv.PdsHostname}"))
            .WithLogger(Logger);
        Protocol = protoBuilder.Build();

        var app = ConsoleApp.Create();
        app.Add<RootCommands>();
        app.Add<AccountCommands>("account");
        await app.RunAsync(args);
    }
}