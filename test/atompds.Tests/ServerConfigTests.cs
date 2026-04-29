using atompds.Config;
using atompds.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace atompds.Tests;

public class ServerConfigTests
{
    [Test]
    public async Task ServerConfig_MapsDidCacheTtlsWithoutSwapping()
    {
        var environment = CreateEnvironment();
        environment.PDS_DID_CACHE_STALE_TTL = 5_000;
        environment.PDS_DID_CACHE_MAX_TTL = 10_000;

        var config = new ServerConfig(environment);

        await Assert.That(config.Identity.CacheStaleTTL).IsEqualTo(5_000);
        await Assert.That(config.Identity.CacheMaxTTL).IsEqualTo(10_000);
    }

    [Test]
    public async Task ServerConfig_RejectsStaleTtlGreaterThanMaxTtl()
    {
        var environment = CreateEnvironment();
        environment.PDS_DID_CACHE_STALE_TTL = 10_000;
        environment.PDS_DID_CACHE_MAX_TTL = 5_000;

        var exception = await Assert.ThrowsAsync<Exception>(() => Task.Run(() => new ServerConfig(environment)));
        await Assert.That(exception.Message).IsEqualTo("PDS_DID_CACHE_STALE_TTL must be less than or equal to PDS_DID_CACHE_MAX_TTL");
    }

    private static ServerEnvironment CreateEnvironment()
    {
        return new ServerEnvironment
        {
            PDS_BLOBSTORE_DISK_LOCATION = Path.Combine(Path.GetTempPath(), "pds-tests", "blocks"),
            PDS_BLOBSTORE_DISK_TMP_LOCATION = Path.Combine(Path.GetTempPath(), "pds-tests", "temp"),
            PDS_JWT_SECRET = TestWebAppFactory.JwtSecret,
            PDS_PLC_ROTATION_KEY_K256_PRIVATE_KEY_HEX = "0000000000000000000000000000000000000000000000000000000000000001",
            PDS_HOSTNAME = "localhost"
        };
    }
}
