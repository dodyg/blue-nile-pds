using System.Text.Json.Serialization;
using atompds.Database;
using Microsoft.AspNetCore.Mvc;

namespace atompds.Controllers;

[ApiController]
[Route(".well-known")]
public class WellKnownController : ControllerBase
{
	private readonly ConfigRepository _configRepository;
	private readonly AccountRepository _accountRepository;

	public WellKnownController(ConfigRepository configRepository, AccountRepository accountRepository)
	{
		_configRepository = configRepository;
		_accountRepository = accountRepository;
	}
	
    [HttpGet("oauth-protected-resource")]
    public async Task<IActionResult> GetOAuthProtectedResource()
	{
		var cfg = await _configRepository.GetConfigAsync();
		return Ok(new WellProtectedResourceResponse(
			cfg.PdsPfx,
			[cfg.PdsPfx], // we are our own auth server
			[],
			["header"],
			"https://atproto.com"));
	}
    
    [HttpGet("oauth-authorization-server")]
	public IActionResult GetOAuthAuthorizationServer()
	{
		return Ok(new
		{
			TODO = "TODO"
		});
	}
	
	[HttpGet("atproto-did")]
	public async Task<IActionResult> GetAtprotoDid()
	{
		// get hostname for request
		var host = Request.Host.Host;
		var did = await _accountRepository.DidByHandleAsync(host);
		if (did == null)
		{
			return NotFound("no user by that handle exists on this PDS");
		}
		
		return Content($"did:web:{host}");
	}
	
	public record WellProtectedResourceResponse(
		[property: JsonPropertyName("resource")]
		string Resource,
		[property: JsonPropertyName("authorization_servers")]
		string[] AuthorizationServers,
		[property: JsonPropertyName("scopes_supported")]
		string[] ScopesSupported,
		[property: JsonPropertyName("bearer_methods_supported")]
		string[] BearerMethodsSupported,
		[property: JsonPropertyName("resource_documentation")]
		string ResourceDocumentation);
}