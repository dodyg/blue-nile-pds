using System.Net.Mail;
using atompds.Model;
using atompds.Pds.Config;
using atompds.Pds.Handle;
using FishyFlip.Lexicon.Com.Atproto.Server;
using FishyFlip.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

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
    
    
    
     [HttpPost("com.atproto.server.createAccount")]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountInput request)
    {
        await ValidateInputsForLocalPds(request);

        string didDoc;
        string accessJwt;
        string refreshJwt;
        
        try
        {
            var cid = "";
            var rev = "";
            var deactivated = false;
            (accessJwt, refreshJwt) = await _accountManager.CreateAccount(request.Did?.Handler, request.Handle?.Handle, request.Email, request.Password, cid, rev, request.InviteCode,
                deactivated);
        }
        catch (Exception e)
        {
            return BadRequest(new HandleNotAvailableErrorDetail("account already exists"));
        }

        var createResponse = new CreateAccountOutput
        {
            AccessJwt = accessJwt,
            RefreshJwt = refreshJwt,
            Handle = new ATHandle(accountInfo.Handle),
            Did = new ATDid(accountInfo.Did)
        };
        return Ok(createResponse);
    }

    private async Task ValidateInputsForLocalPds(CreateAccountInput createAccountInput)
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
        
        var signingKey = GenerateKeyPair();

        if (createAccountInput.Did != null)
        {
            // if did != requested, throw error
            throw new ErrorDetailException(new InvalidRequestErrorDetail("TEMP"));
        }
        else
        {
            // TODO: need to load keypair for either pldRotationKey.privateKeyHex (Secp256k1) or pldRotationKey.keyId (KmsKeyPair)
        }
    }

    private AsymmetricCipherKeyPair GenerateKeyPair()
    {
        var curve = ECNamedCurveTable.GetByName("secp256k1");
        var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H, curve.GetSeed());
        
        var random = new SecureRandom();
        var keyParams = new ECKeyGenerationParameters(domain, random);
        
        var generator = new ECKeyPairGenerator("ECDSA");
        generator.Init(keyParams);
        var keyPair = generator.GenerateKeyPair();

        return keyPair;
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