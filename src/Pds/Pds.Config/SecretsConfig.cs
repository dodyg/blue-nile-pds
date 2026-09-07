using BlueNilePds.Core.Crypto.Secp256k1;

namespace BlueNilePds.Pds.Config;

public record SecretsConfig
{
    public required string JwtSecret { get; init; }
    public required Secp256k1Keypair PlcRotationKey { get; set; }
}