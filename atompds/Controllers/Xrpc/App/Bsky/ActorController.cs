using atompds.Auth;
using atompds.Database;
using atompds.Model;
using FishyFlip.Lexicon.App.Bsky.Actor;
using FishyFlip.Models;
using Microsoft.AspNetCore.Mvc;

namespace atompds.Controllers.Xrpc.App.Bsky;

[ApiController]
[Route("xrpc")]
public class ActorController : ControllerBase
{
    private readonly AccountRepository _accountRepository;

    public ActorController(AccountRepository accountRepository)
    {
        _accountRepository = accountRepository;
    }
    
    [HttpGet("app.bsky.actor.getPreferences")]
    [JwtAuthorize]
    public Task<IActionResult> GetPreferences()
    {
        return Task.FromResult<IActionResult>(Ok(new GetPreferencesOutput
        {
            Preferences = []
        }));
    }
    
    [HttpPost("app.bsky.actor.putPreferences")]
    [JwtAuthorize]
    public Task<IActionResult> PutPreferences([FromBody] PutPreferencesInput request)
    {
        return Task.FromResult<IActionResult>(Ok());
    }
    

    [HttpGet("app.bsky.actor.getProfile")]
    public async Task<IActionResult> GetProfile([FromQuery] string actor)
    {
        var match = await _accountRepository.GetAccountAsync(actor);
        if (match == null)
        {
            return BadRequest(new InvalidRequestErrorDetail("actor not found"));
        }

        return Ok(new ProfileViewDetailed
        {
            Did = new ATDid(match.Did),
            Handle = new ATHandle(match.Handle),
        });
    }
    
    [HttpGet("app.bsky.actor.getProfiles")]
    public async Task<IActionResult> GetProfiles([FromQuery] string[] actors)
    {
        if (actors.Length == 0)
        {
            return BadRequest(new InvalidRequestErrorDetail("actors is required"));
        }

        var matches = await _accountRepository.GetAccountsAsync(actors);
        if (matches.Length == 0)
        {
            return BadRequest(new InvalidRequestErrorDetail("no actors found"));
        }
        

        return Ok(new GetProfilesOutput
        {
            Profiles = matches.Select(m => new ProfileViewDetailed
            {
                Did = new ATDid(m.Did),
                Handle = new ATHandle(m.Handle),
            }).ToList()
        });
    }
}