using atompds.Database;
using atompds.Model;
using FishyFlip.Lexicon.Com.Atproto.Server;
using FishyFlip.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace atompds.Controllers.Xrpc.Com.Atproto;

[ApiController]
[Route("xrpc")]
public class ServerController : ControllerBase
{
    private readonly ILogger<ServerController> _logger;
    private readonly ConfigRepository _configRepository;
    private readonly AccountRepository _accountRepository;
    private readonly JwtHandler _jwtHandler;

    public ServerController(ILogger<ServerController> logger, 
        ConfigRepository configRepository, 
        AccountRepository accountRepository,
        JwtHandler jwtHandler)
    {
        _logger = logger;
        _configRepository = configRepository;
        _accountRepository = accountRepository;
        _jwtHandler = jwtHandler;
    }
    
    [HttpGet("com.atproto.server.describeServer")]
    public async Task<IActionResult> DescribeServer()
    {
        var cfg = await _configRepository.GetConfigAsync();
        return Ok(new DescribeServerOutput
        {
            Did = new ATDid(cfg.PdsDid),
            AvailableUserDomains = cfg.AvailableUserDomains.ToList(),
            InviteCodeRequired = true
        });
    }
    
    [HttpPost("com.atproto.server.createSession")]
    [EnableRateLimiting(Program.FixedWindowLimiterName)]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionInput request)
    {
        try
        {
            var response = await _jwtHandler.GenerateJwtToken(request);
            return Ok(response);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to create session");
            return Unauthorized();
        }
    }
    
    [HttpPost("com.atproto.server.createAccount")]
    [EnableRateLimiting(Program.FixedWindowLimiterName)]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountInput request)
    {
        try
        {
            var config = await _configRepository.GetConfigAsync();
            
            if (string.IsNullOrWhiteSpace(request.InviteCode))
            {
                throw new ErrorDetailException(new InvalidInviteCodeErrorDetail("missing invite code"));
                return BadRequest(new InvalidInviteCodeErrorDetail("missing invite code"));
            }

            if (request.InviteCode != "kweh")
            {
                throw new ErrorDetailException(new InvalidInviteCodeErrorDetail("invalid invite code"));
                return BadRequest(new InvalidInviteCodeErrorDetail("invalid invite code"));
            }
            
            if (!string.IsNullOrWhiteSpace(request.Did?.Handler))
            {
                throw new ErrorDetailException(new IncompatibleDidDocErrorDetail("DID is not allowed to be set"));
                return BadRequest(new IncompatibleDidDocErrorDetail("DID is not allowed to be set"));
            }
            
            // ensure handle is not empty
            if (string.IsNullOrWhiteSpace(request.Handle?.Handle))
            {
                throw new ErrorDetailException(new InvalidHandleErrorDetail("missing handle"));
                return BadRequest(new InvalidHandleErrorDetail("missing handle"));
            }
            
            // ensure handle ends with available domain
            if (!config.AvailableUserDomains.Any(x => request.Handle.Handle.EndsWith(x)))
            {
                throw new ErrorDetailException(new UnsupportedDomainErrorDetail("handle must end with an available domain"));
                return BadRequest(new UnsupportedDomainErrorDetail("handle must end with an available domain"));
            }
            
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            {
                throw new ErrorDetailException(new InvalidPasswordErrorDetail("invalid password"));
                return BadRequest(new InvalidPasswordErrorDetail("invalid password"));
            }

            AccountRepository.AccountInfo accountInfo;
            try
            {
                accountInfo = await _accountRepository.CreateAccountAsync(request);
            }
            catch (DuplicateAccountException e)
            {
                return BadRequest(new HandleNotAvailableErrorDetail("account already exists"));
            }

            var response = await _jwtHandler.GenerateJwtToken(new CreateSessionInput
            {
                Identifier = accountInfo.Did,
                Password = request.Password
            });
            
            var createResponse = new CreateAccountOutput
            {
                AccessJwt = response.AccessJwt,
                RefreshJwt = response.RefreshJwt,
                Handle = new ATHandle(accountInfo.Handle),
                Did = new ATDid(accountInfo.Did)
            };
            return Ok(createResponse);
        }
        catch (Exception e)
        {
            var guid = Guid.NewGuid().ToString();
            _logger.LogError(e, "{prefix} Failed to create account", guid);
            return BadRequest(new InvalidRequestErrorDetail($"failed to create account, report this using the following code: {guid}"));
        }
    }
}