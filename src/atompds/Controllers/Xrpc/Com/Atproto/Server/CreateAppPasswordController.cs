using System.Security.Cryptography;
using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class CreateAppPasswordController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly AppPasswordStore _appPasswordStore;
    private readonly ILogger<CreateAppPasswordController> _logger;

    public CreateAppPasswordController(
        AccountRepository accountRepository,
        AppPasswordStore appPasswordStore,
        ILogger<CreateAppPasswordController> logger)
    {
        _accountRepository = accountRepository;
        _appPasswordStore = appPasswordStore;
        _logger = logger;
    }

    [HttpPost("com.atproto.server.createAppPassword")]
    [AccessPrivileged]
    public async Task<IActionResult> CreateAppPasswordAsync([FromBody] CreateAppPasswordInput request)
    {
        var auth = HttpContext.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("name is required"));
        }

        var existing = await _appPasswordStore.ListAppPasswordsAsync(did);
        if (existing.Any(ap => ap.Name == request.Name))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("App password with this name already exists"));
        }

        var password = GenerateAppPassword();
        var privileged = request.Privileged ?? false;
        await _appPasswordStore.CreateAppPasswordAsync(did, request.Name, password, privileged);

        return Ok(new
        {
            name = request.Name,
            password
        });
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
            {
                segments.Add(new string(current));
            }
        }

        return string.Join("-", segments);
    }
}

public class CreateAppPasswordInput
{
    public string? Name { get; set; }
    public bool? Privileged { get; set; }
}
