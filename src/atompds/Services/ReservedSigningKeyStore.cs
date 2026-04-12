using Crypto.Secp256k1;

namespace atompds.Services;

public class ReservedSigningKeyStore
{
    private static readonly TimeSpan ReservationTtl = TimeSpan.FromHours(1);
    private readonly ILogger<ReservedSigningKeyStore> _logger;
    private readonly IScratchCache _scratchCache;

    public ReservedSigningKeyStore(
        IScratchCache scratchCache,
        ILogger<ReservedSigningKeyStore> logger)
    {
        _scratchCache = scratchCache;
        _logger = logger;
    }

    public async Task<string> ReserveAsync(string? did = null)
    {
        var keypair = Secp256k1Keypair.Create(true);
        var cacheKey = GetCacheKey(did ?? keypair.Did());
        await _scratchCache.SetAsync(cacheKey, Convert.ToHexString(keypair.Export()), ReservationTtl);
        return keypair.Did();
    }

    public async Task<Secp256k1Keypair?> ConsumeAsync(string did)
    {
        var cacheKey = GetCacheKey(did);
        var privateKeyHex = await _scratchCache.GetAsync(cacheKey);
        if (string.IsNullOrWhiteSpace(privateKeyHex))
        {
            return null;
        }

        await _scratchCache.DeleteAsync(cacheKey);
        return Secp256k1Keypair.Import(privateKeyHex, true);
    }

    private static string GetCacheKey(string did) => $"reserved-signing-key:{did}";
}
