using Crypto.Secp256k1;

namespace Config;

public record SecretsConfig
{
    public required string JwtSecret { get; init; }
    public required Secp256k1Keypair PlcRotationKey { get; set; }
}