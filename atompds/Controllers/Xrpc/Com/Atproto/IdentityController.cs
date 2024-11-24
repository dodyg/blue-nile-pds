using System.Text.Json.Serialization;
using atompds.Database;
using FishyFlip.Lexicon.Com.Atproto.Identity;
using FishyFlip.Models;
using Microsoft.AspNetCore.Mvc;

namespace atompds.Controllers.Xrpc.Com.Atproto;

[ApiController]
[Route("xrpc")]
public class IdentityController : ControllerBase
{
    private readonly AccountRepository _accountRepository;

    public IdentityController(AccountRepository accountRepository)
    {
        _accountRepository = accountRepository;
    }
    
    [HttpGet("com.atproto.identity.resolveHandle")]
    public async Task<IActionResult> ResolveHandle([FromQuery] string handle)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            return BadRequest("missing or invalid handle");
        }

        var did = await _accountRepository.DidByHandleAsync(handle);
        if (did == null)
        {
            return NotFound("no user by that handle exists on this PDS");
        }

        return Ok(new ResolveHandleOutput
        {
            Did = new ATDid(did)
        });
    }
    
    public record HealthResponse(
        [property: JsonPropertyName("version")]
        string Version);
}