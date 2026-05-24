using System.Net.Http.Headers;
using atompds.Config;
using System.Text;
using Config;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xrpc;

namespace atompds.Services;

public class EntrywayRelayService
{
    private static readonly HashSet<string> SkippedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Content-Length",
        "Content-Type",
        "Host"
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<EntrywayRelayService> _logger;
    private readonly ServerEnvironment _serverEnvironment;
    private readonly ServiceJwtBuilder _serviceJwtBuilder;

    public EntrywayRelayService(
        HttpClient httpClient,
        ServerEnvironment serverEnvironment,
        ServiceJwtBuilder serviceJwtBuilder,
        ILogger<EntrywayRelayService> logger)
    {
        _httpClient = httpClient;
        _serverEnvironment = serverEnvironment;
        _serviceJwtBuilder = serviceJwtBuilder;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_serverEnvironment.PDS_OAUTH_ENTRYWAY_URL) &&
        !string.IsNullOrWhiteSpace(_serverEnvironment.PDS_OAUTH_ENTRYWAY_DID);

    public string? EntrywayUrl => _serverEnvironment.PDS_OAUTH_ENTRYWAY_URL?.TrimEnd('/');
    public string? EntrywayDid => _serverEnvironment.PDS_OAUTH_ENTRYWAY_DID;

    public string BuildAbsoluteUrl(string relativePathAndQuery)
    {
        if (!IsConfigured || EntrywayUrl == null)
        {
            throw new InvalidOperationException("OAuth entryway is not configured");
        }

        return $"{EntrywayUrl}{relativePathAndQuery}";
    }

    public async Task<IResult> ForwardJsonAsync(
        HttpRequest incoming,
        string relativePath,
        string rawJsonBody,
        string? actingDid = null,
        string? lxm = null,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(incoming, HttpMethod.Post, relativePath, actingDid, lxm);
        request.Content = new StringContent(rawJsonBody, Encoding.UTF8, "application/json");
        return await SendAsync(request, cancellationToken);
    }

    public async Task<IResult> ForwardFormAsync(
        HttpRequest incoming,
        string relativePath,
        IFormCollection form,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(incoming, HttpMethod.Post, relativePath);
        request.Content = new FormUrlEncodedContent(form.SelectMany(kvp =>
            kvp.Value.Select(value => new KeyValuePair<string, string>(kvp.Key, value))));
        return await SendAsync(request, cancellationToken);
    }

    public async Task<IResult> ForwardWithoutBodyAsync(
        HttpRequest incoming,
        HttpMethod method,
        string relativePath,
        string? actingDid = null,
        string? lxm = null,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(incoming, method, relativePath, actingDid, lxm);
        return await SendAsync(request, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(
        HttpRequest incoming,
        HttpMethod method,
        string relativePath,
        string? actingDid = null,
        string? lxm = null)
    {
        if (!IsConfigured || EntrywayUrl == null)
        {
            throw new InvalidOperationException("OAuth entryway is not configured");
        }

        var request = new HttpRequestMessage(method, BuildAbsoluteUrl(relativePath));
        CopyHeaders(incoming, request);

        if (!string.IsNullOrWhiteSpace(actingDid))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                _serviceJwtBuilder.CreateServiceJwt(actingDid, EntrywayDid!, lxm));
        }

        return request;
    }

    private static void CopyHeaders(HttpRequest incoming, HttpRequestMessage outgoing)
    {
        foreach (var header in incoming.Headers)
        {
            if (SkippedHeaders.Contains(header.Key))
            {
                continue;
            }

            TryAddHeader(outgoing.Headers, header.Key, header.Value);
        }
    }

    private static void TryAddHeader(HttpHeaders headers, string key, StringValues values)
    {
        if (values.Count == 0)
        {
            return;
        }

        headers.TryAddWithoutValidation(key, values.AsEnumerable());
    }

    private async Task<IResult> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = response.Content == null
                ? null
                : await response.Content.ReadAsStringAsync(cancellationToken);

            if (string.IsNullOrEmpty(content))
            {
                return Results.StatusCode((int)response.StatusCode);
            }

            var contentType = response.Content.Headers.ContentType?.ToString();
            return Results.Content(content, contentType, statusCode: (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to relay request to configured OAuth entryway");
            throw new XRPCError(502, "UpstreamFailure", "Failed to relay request to configured OAuth entryway", ex);
        }
    }
}
