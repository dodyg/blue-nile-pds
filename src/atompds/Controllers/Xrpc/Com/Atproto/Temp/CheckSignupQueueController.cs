using atompds.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Temp;

[ApiController]
[Route("xrpc")]
public class CheckSignupQueueController : ControllerBase
{
    private readonly AuthVerifier _authVerifier;

    public CheckSignupQueueController(AuthVerifier authVerifier)
    {
        _authVerifier = authVerifier;
    }

    [HttpGet("com.atproto.temp.checkSignupQueue")]
    public async Task<IActionResult> CheckSignupQueueAsync()
    {
        await _authVerifier.ValidateAccessTokenAsync(HttpContext,
        [
            AuthVerifier.ScopeMap[AuthVerifier.AuthScope.SignupQueued]
        ]);

        return Ok(new
        {
            activated = true
        });
    }
}
