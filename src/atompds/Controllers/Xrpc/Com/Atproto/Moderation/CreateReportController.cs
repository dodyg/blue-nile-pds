using System.Net.Http.Headers;
using System.Text.Json;
using atompds.Config;
using atompds.Middleware;
using atompds.Services;
using Config;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Xrpc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Moderation;

[ApiController]
[Route("xrpc")]
public class CreateReportController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CreateReportController> _logger;
    private readonly ServerEnvironment _serverEnvironment;
    private readonly ServiceJwtBuilder _serviceJwtBuilder;

    public CreateReportController(
        HttpClient httpClient,
        ILogger<CreateReportController> logger,
        ServerEnvironment serverEnvironment,
        ServiceJwtBuilder serviceJwtBuilder)
    {
        _httpClient = httpClient;
        _logger = logger;
        _serverEnvironment = serverEnvironment;
        _serviceJwtBuilder = serviceJwtBuilder;
    }

    [HttpPost("com.atproto.moderation.createReport")]
    [AccessStandard]
    public async Task<IActionResult> CreateReportAsync([FromBody] JsonElement input)
    {
        if (string.IsNullOrWhiteSpace(_serverEnvironment.PDS_REPORT_SERVICE_URL) ||
            string.IsNullOrWhiteSpace(_serverEnvironment.PDS_REPORT_SERVICE_DID))
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Report service is not configured"));
        }

        try
        {
            var auth = HttpContext.GetAuthOutput();
            var jwt = _serviceJwtBuilder.CreateServiceJwt(
                auth.AccessCredentials.Did,
                _serverEnvironment.PDS_REPORT_SERVICE_DID,
                "com.atproto.moderation.createReport");

            var url = $"{_serverEnvironment.PDS_REPORT_SERVICE_URL}/xrpc/com.atproto.moderation.createReport";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            request.Content = new StringContent(input.GetRawText(), System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            return new ContentResult
            {
                Content = content,
                StatusCode = (int)response.StatusCode,
                ContentType = response.Content.Headers.ContentType?.ToString()
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to forward moderation report");
            throw;
        }
    }
}
