using atompds.Config;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
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
            catch
            {
            }
        }

        base.Dispose(disposing);
    }
}
