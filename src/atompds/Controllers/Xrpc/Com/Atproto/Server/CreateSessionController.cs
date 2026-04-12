using System.Text.Json;
using AccountManager;
using AccountManager.Db;
using atompds.Services;
using atompds.Utils;
using CarpaNet;
using CommonWeb;
using ComAtproto.Server;
using Config;
using Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class CreateSessionController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly EntrywayRelayService _entrywayRelayService;
    private readonly IdentityConfig _identityConfig;
    private readonly IdResolver _idResolver;
    private readonly ILogger<CreateSessionController> _logger;

    public CreateSessionController(
        AccountRepository accountRepository,
        IdentityConfig identityConfig,
        IdResolver idResolver,
        EntrywayRelayService entrywayRelayService,
        ILogger<CreateSessionController> logger)
    {
        _accountRepository = accountRepository;
        _identityConfig = identityConfig;
        _idResolver = idResolver;
        _entrywayRelayService = entrywayRelayService;
        _logger = logger;
    }

    [HttpPost("com.atproto.server.createSession")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> CreateSessionAsync([FromBody] JsonElement request)
    {
        if (_entrywayRelayService.IsConfigured)
        {
            return await _entrywayRelayService.ForwardJsonAsync(
                HttpContext.Request,
                "/xrpc/com.atproto.server.createSession",
                request.GetRawText(),
                cancellationToken: HttpContext.RequestAborted);
        }

        var identifier = TryGetString(request, "identifier");
        var password = TryGetString(request, "password");
        var allowTakendown = TryGetBoolean(request, "allowTakendown") == true;
        if (identifier == null || password == null)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Identifier and password are required"));
        }

        var login = await _accountRepository.LoginAsync(identifier, password, allowTakendown);
        var creds = await _accountRepository.CreateSessionAsync(login.Account.Did, login.AppPasswordName, login.AppPasswordScope);
        var didDoc = await DidDocForSessionAsync(login.Account.Did);
        var (active, status) = AccountStore.FormatAccountStatus(login.Account);

        return Ok(new CreateSessionOutput
        {
            AccessJwt = creds.AccessJwt,
            RefreshJwt = creds.RefreshJwt,
            Handle = new ATHandle(login.Account.Handle ?? Constants.INVALID_HANDLE),
            Did = new ATDid(login.Account.Did),
            DidDoc = didDoc?.ToJsonElement(),
            Email = login.Account.Email,
            EmailConfirmed = login.Account.EmailConfirmedAt != null,
            EmailAuthFactor = null,
            Active = active,
            Status = status.ToString()
        });
    }

    private async Task<DidDocument?> DidDocForSessionAsync(string did, bool forceRefresh = false)
    {
        if (!_identityConfig.EnableDidDocWithSession)
        {
            return null;
        }

        return await SafeResolveDidDocAsync(did, forceRefresh);
    }

    private async Task<DidDocument?> SafeResolveDidDocAsync(string did, bool forceRefresh = false)
    {
        try
        {
            return await _idResolver.DidResolver.ResolveAsync(did, forceRefresh);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to resolve did doc: {did}", did);
            return null;
        }
    }

    private static string? TryGetString(JsonElement body, string propertyName)
    {
        return body.ValueKind == JsonValueKind.Object &&
               body.TryGetProperty(propertyName, out var property) &&
               property.ValueKind != JsonValueKind.Null
            ? property.GetString()
            : null;
    }

    private static bool? TryGetBoolean(JsonElement body, string propertyName)
    {
        return body.ValueKind == JsonValueKind.Object &&
               body.TryGetProperty(propertyName, out var property) &&
               (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
            ? property.GetBoolean()
            : null;
    }
}
