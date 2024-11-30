using System.Net.Mail;
using atompds.Model;
using atompds.Pds.ActorStore.Db;
using atompds.Pds.Config;
using atompds.Pds.Handle;
using Crypto.Secp256k1;
using DidLib;
using FishyFlip.Lexicon.Com.Atproto.Server;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace atompds.Controllers.Xrpc.Com.Atproto.Server;

[ApiController]
[Route("xrpc")]
public class CreateAccountController : ControllerBase
{
    private readonly ILogger<CreateAccountController> _logger;
    private readonly AccountManager.AccountManager _accountManager;
    private readonly IdentityConfig _identityConfig;
    private readonly ServiceConfig _serviceConfig;
    private readonly InvitesConfig _invitesConfig;
    private readonly HttpClient _httpClient;
    private readonly Handle _handle;
    private readonly ActorStore _actorStore;
    private readonly SecretsConfig _secretsConfig;

    public CreateAccountController(ILogger<CreateAccountController> logger,
        AccountManager.AccountManager accountManager,
        IdentityConfig identityConfig,
        ServiceConfig serviceConfig,
        InvitesConfig invitesConfig,
        HttpClient httpClient,
        Handle handle,
        ActorStore actorStore,
        SecretsConfig secretsConfig)
    {
        _logger = logger;
        _accountManager = accountManager;
        _identityConfig = identityConfig;
        _serviceConfig = serviceConfig;
        _invitesConfig = invitesConfig;
        _httpClient = httpClient;
        _handle = handle;
        _actorStore = actorStore;
        _secretsConfig = secretsConfig;
    }
    
    
     [HttpPost("com.atproto.server.createAccount")]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountInput request)
    {
        var validatedInputs = await ValidateInputsForLocalPds(request);

        string didDoc;
        string accessJwt;
        string refreshJwt;
        _actorStore.Create(validatedInputs.did, validatedInputs.signingKey);
    }

    private async Task<(string did, string handle, string Email, string? Password, string? InviteCode, Secp256k1Keypair signingKey, AtProtoOp? plcOp, bool deactivated)> ValidateInputsForLocalPds(CreateAccountInput createAccountInput)
    {
        if (createAccountInput.PlcOp != null)
        {
            throw new ErrorDetailException(new InvalidRequestErrorDetail("Unsupported input: \"plcOp\""));
        }
        
        if (_invitesConfig.Required && string.IsNullOrWhiteSpace(createAccountInput.InviteCode))
        {
            throw new ErrorDetailException(new InvalidInviteCodeErrorDetail("No invite code provided"));
        }
        
        if (createAccountInput.Email == null)
        {
            throw new ErrorDetailException(new InvalidRequestErrorDetail("Email is required"));
        }
        if (!IsValidEmail(createAccountInput.Email) || await IsDisposableEmail(createAccountInput.Email))
        {
            throw new ErrorDetailException(new InvalidRequestErrorDetail("This email address is not supported, please use a different email."));
        }

        var handle = await _handle.NormalizeAndValidateHandle(createAccountInput.Handle!.Handle, createAccountInput.Did?.Handler, false);

        if (_invitesConfig.Required && createAccountInput.InviteCode != null)
        {
            await _accountManager.EnsureInviteIsAvailable(createAccountInput.InviteCode);
        }
        
        var handleAcct = await _accountManager.GetAccount(handle);
        var emailAcct = await _accountManager.GetAccount(createAccountInput.Email);
        if (handleAcct != null)
        {
            throw new ErrorDetailException(new HandleNotAvailableErrorDetail($"Handle already taken: {handle}"));
        }
        else if (emailAcct != null)
        {
            throw new ErrorDetailException(new InvalidRequestErrorDetail($"Email already taken: {createAccountInput.Email}"));
        }

        var signingKey = Secp256k1Keypair.Create(true);

        string did;
        AtProtoOp plcOp;
        bool deactivated = false;
        if (createAccountInput.Did != null)
        {
            // if did != requested, throw error
            deactivated = true;
            throw new ErrorDetailException(new InvalidRequestErrorDetail("TEMP"));
        }
        else
        {
            (did, plcOp) = await FormatDidAndPlcOp(handle, createAccountInput, signingKey);
        }
        
        return (did, handle, createAccountInput.Email, createAccountInput.Password, createAccountInput.InviteCode, signingKey, plcOp, deactivated);
    }
    
    public async Task<(string Did, object? PlcOp)> FormatDidAndPlcOp(string handle, CreateAccountInput createAccountInput, Secp256k1Keypair signingKey)
    {
        string[] rotationKeys = [_secretsConfig.PlcRotationKey.Did()];
        if (_identityConfig.RecoveryDidKey != null)
        {
            rotationKeys = [_identityConfig.RecoveryDidKey, ..rotationKeys];
        }
        if (createAccountInput.RecoveryKey != null)
        {
            rotationKeys = [createAccountInput.RecoveryKey, ..rotationKeys];
        }
        
        // plcCreate ...
        var plcCreate = await DidLib.Operations.CreateOp(signingKey.Did(), handle, _serviceConfig.PublicUrl, rotationKeys, _secretsConfig.PlcRotationKey);
        return (plcCreate.Did, plcCreate.Op);
    }
    
    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private record DisposableResponse([property: JsonProperty("disposable")] bool Disposable);
    private async Task<bool> IsDisposableEmail(string email)
    {
        try
        {        
            var response = await _httpClient.GetAsync($"https://open.kickbox.com/v1/disposable/{email}");
            var content = await response.Content.ReadAsStringAsync();
            var disposableResponse = JsonConvert.DeserializeObject<DisposableResponse>(content);
            if (disposableResponse == null)
            {
                return false;
            }
            
            return disposableResponse.Disposable;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to check if email is disposable");
            return false;
        }
    }
}