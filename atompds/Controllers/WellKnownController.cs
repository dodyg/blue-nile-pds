using System.Text.Json.Serialization;
using atompds.Pds.Config;
using Microsoft.AspNetCore.Mvc;

namespace atompds.Controllers;

[ApiController]
[Route(".well-known")]
public class WellKnownController : ControllerBase
{
	private readonly ServiceConfig _serviceConfig;
	private readonly Pds.AccountManager.AccountManager _accountManager;
	public WellKnownController(ServiceConfig serviceConfig, Pds.AccountManager.AccountManager accountManager)
	{
		_serviceConfig = serviceConfig;
		_accountManager = accountManager;
	}
	
    [HttpGet("oauth-protected-resource")]
    public async Task<IActionResult> GetOAuthProtectedResource()
	{
		return Ok(new WellProtectedResourceResponse(
			_serviceConfig.PublicUrl,
			[_serviceConfig.PublicUrl], // we are our own auth server
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
		var acc = await _accountManager.GetAccount(host);
		if (acc?.Handle == null)
		{
			return NotFound("no user by that handle exists on this PDS");
		}
		
		return Content($"did:web:{acc.Handle}");
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