using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace atompds.Tests.Infrastructure;

public class TestWebAppFactory : WebApplicationFactory<Program>
{
    public const string JwtSecret = "0123456789abcdef0123456789abcdef";
    public const string ServiceDid = "did:web:localhost";
    public const string AdminUser = "admin";
    public const string AdminPassword = "secret";
    public const string TestDid = "did:plc:testtesttesttesttesttesttesttest";
    public const string TestHandle = "test.test";
    private const string PlcDirectoryUrl = "https://plc.test.local";

    private readonly string _tempDir;

    public TestWebAppFactory()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"atompds-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "blocks"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "temp"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "actors"));
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Config:PDS_HOSTNAME"] = "localhost",
                ["Config:PDS_PORT"] = "2583",
                ["Config:PDS_DATA_DIRECTORY"] = _tempDir,
                ["Config:PDS_ACCOUNT_DB_LOCATION"] = Path.Combine(_tempDir, "account.sqlite"),
                ["Config:PDS_SEQUENCER_DB_LOCATION"] = Path.Combine(_tempDir, "sequencer.sqlite"),
                ["Config:PDS_DID_CACHE_DB_LOCATION"] = Path.Combine(_tempDir, "did_cache.sqlite"),
                ["Config:PDS_ACTOR_STORE_DIRECTORY"] = Path.Combine(_tempDir, "actors"),
                ["Config:PDS_BLOBSTORE_DISK_LOCATION"] = Path.Combine(_tempDir, "blocks"),
                ["Config:PDS_BLOBSTORE_DISK_TMP_LOCATION"] = Path.Combine(_tempDir, "temp"),
                ["Config:PDS_JWT_SECRET"] = JwtSecret,
                ["Config:PDS_PLC_ROTATION_KEY_K256_PRIVATE_KEY_HEX"] = "0000000000000000000000000000000000000000000000000000000000000001",
                ["Config:PDS_DID_PLC_URL"] = PlcDirectoryUrl,
                ["Config:PDS_RATE_LIMITS_ENABLED"] = "false",
                ["Config:PDS_INVITE_REQUIRED"] = "false",
                ["Config:PDS_DEV_MODE"] = "true",
                ["Config:PDS_SERVICE_HANDLE_DOMAINS:0"] = ".test",
                ["Config:PDS_CONTACT_EMAIL"] = "test@test.test",
                ["Config:PDS_PRIVACY_POLICY_URL"] = "https://test.test/privacy",
                ["Config:PDS_TERMS_OF_SERVICE_URL"] = "https://test.test/terms",
                ["Config:PDS_HOME_URL"] = "https://test.test",
                ["Config:PDS_SUPPORT_URL"] = "https://test.test/support",
                ["Config:PDS_LOGO_URL"] = "https://test.test/logo.png",
            });
        });
        builder.ConfigureServices(services =>
        {
            services.AddTransient(_ => new HttpClient(new TestExternalHttpMessageHandler())
            {
                Timeout = TimeSpan.FromSeconds(5)
            });
        });
        return base.CreateHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"Failed to delete test temp directory '{_tempDir}': {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.Error.WriteLine($"Failed to delete test temp directory '{_tempDir}': {ex.Message}");
            }
        }

        base.Dispose(disposing);
    }

    private sealed class TestExternalHttpMessageHandler : HttpMessageHandler
    {
        private static readonly ConcurrentDictionary<string, string> PlcDocuments = new();
        private static readonly string PlcDirectoryHost = new Uri(PlcDirectoryUrl).Host;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is null)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("missing request uri")
                };
            }

            if (request.RequestUri.Host.Equals("open.kickbox.com", StringComparison.OrdinalIgnoreCase))
            {
                return Json(HttpStatusCode.OK, "{\"disposable\":false}");
            }

            if (request.RequestUri.Host.Equals("api.hcaptcha.com", StringComparison.OrdinalIgnoreCase))
            {
                return Json(HttpStatusCode.OK, "{\"success\":true}");
            }

            if (request.RequestUri.Host.Equals(PlcDirectoryHost, StringComparison.OrdinalIgnoreCase))
            {
                return await HandlePlcAsync(request, cancellationToken);
            }

            if (request.RequestUri.Host.Equals("bsky.network", StringComparison.OrdinalIgnoreCase))
            {
                return Json(HttpStatusCode.OK, "{}");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent($"Unhandled external request: {request.RequestUri}", Encoding.UTF8, "text/plain")
            };
        }

        private static async Task<HttpResponseMessage> HandlePlcAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var did = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath.Trim('/'));
            if (string.IsNullOrWhiteSpace(did))
            {
                return Json(HttpStatusCode.BadRequest, "{\"error\":\"missing did\"}");
            }

            if (request.Method == HttpMethod.Get)
            {
                return PlcDocuments.TryGetValue(did, out var document)
                    ? Json(HttpStatusCode.OK, document)
                    : Json(HttpStatusCode.NotFound, "{\"error\":\"not found\"}");
            }

            if (request.Method == HttpMethod.Post)
            {
                var opJson = await request.Content!.ReadAsStringAsync(cancellationToken);
                PlcDocuments[did] = BuildDidDocumentJson(did, opJson);
                return Json(HttpStatusCode.OK, "{}");
            }

            return Json(HttpStatusCode.MethodNotAllowed, "{\"error\":\"unsupported method\"}");
        }

        private static string BuildDidDocumentJson(string did, string opJson)
        {
            using var document = JsonDocument.Parse(opJson);
            var root = document.RootElement;

            var alsoKnownAs = root.TryGetProperty("alsoKnownAs", out var aliasesElement) && aliasesElement.ValueKind == JsonValueKind.Array
                ? aliasesElement.EnumerateArray().Select(element => element.GetString()).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().ToArray()
                : [];

            string? publicKeyMultibase = null;
            if (root.TryGetProperty("verificationMethods", out var verificationMethods) &&
                verificationMethods.ValueKind == JsonValueKind.Object &&
                verificationMethods.TryGetProperty("atproto", out var atprotoKey))
            {
                var didKey = atprotoKey.GetString();
                publicKeyMultibase = didKey?.StartsWith("did:key:", StringComparison.Ordinal) == true
                    ? didKey[8..]
                    : didKey;
            }

            string? serviceEndpoint = null;
            if (root.TryGetProperty("services", out var servicesElement) &&
                servicesElement.ValueKind == JsonValueKind.Object &&
                servicesElement.TryGetProperty("atproto_pds", out var pdsService) &&
                pdsService.ValueKind == JsonValueKind.Object &&
                pdsService.TryGetProperty("endpoint", out var endpointElement))
            {
                serviceEndpoint = endpointElement.GetString();
            }

            var didDocument = new
            {
                id = did,
                alsoKnownAs,
                verificationMethod = new[]
                {
                    new
                    {
                        id = $"{did}#atproto",
                        type = "Multikey",
                        controller = did,
                        publicKeyMultibase
                    }
                },
                service = new[]
                {
                    new
                    {
                        id = $"{did}#atproto_pds",
                        type = "AtprotoPersonalDataServer",
                        serviceEndpoint = serviceEndpoint ?? "https://localhost"
                    }
                }
            };

            return JsonSerializer.Serialize(didDocument);
        }

        private static HttpResponseMessage Json(HttpStatusCode statusCode, string json)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }
}
