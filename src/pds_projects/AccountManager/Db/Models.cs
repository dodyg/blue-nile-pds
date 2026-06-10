using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountManager.Db;

[Table(TableName)]
public class Account
{
    public const string TableName = "account";

    [StringLength(2048)] public required string Did { get; set; }
    [StringLength(2048)] public required string Email { get; set; }
    [StringLength(2048)] public required string PasswordSCrypt { get; set; }
    public DateTime? EmailConfirmedAt { get; set; }
    public bool InvitesDisabled { get; set; }

    public virtual Actor? Actor { get; set; }
}

[Table(TableName)]
public class Actor
{
    public const string TableName = "actor";

    [StringLength(2048)] public required string Did { get; set; }
    [StringLength(2048)] public required string Handle { get; set; }
    public required DateTime CreatedAt { get; set; }
    [StringLength(2048)] public required string? TakedownRef { get; set; }
    public DateTime? DeactivatedAt { get; set; }
    public DateTime? DeleteAfter { get; set; }

    public virtual Account? Account { get; set; }
}

[Table(TableName)]
public class Device
{
    public const string TableName = "device";

    [Key,StringLength(2048)] public required string Id { get; set; }
    [StringLength(2048)] public required string AccountDid { get; set; }
    [StringLength(2048)] public required string SessionId { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime LastSeenAt { get; set; }
}

[Table(TableName)]
public class AccountDevice
{
    public const string TableName = "account_device";

    [StringLength(2048)] public required string Did { get; set; }
    [StringLength(2048)] public required string DeviceId { get; set; }
}

[Table(TableName)]
public class AuthorizationRequest
{
    public const string TableName = "authorization_request";

    [Key,StringLength(2048)] public required string Id { get; set; }
    [StringLength(2048)] public string? Did { get; set; }
    [StringLength(2048)] public required string Parameters { get; set; }
    public required DateTime ExpiresAt { get; set; }
}

[Table(TableName)]
public class AuthorizedClient
{
    public const string TableName = "authorized_client";

    [Key,StringLength(2048)] public required string Id { get; set; }
    [StringLength(2048)] public required string Did { get; set; }
    [StringLength(2048)] public required string ClientId { get; set; }
    [StringLength(2048)] public required string Scope { get; set; }
    public required DateTime CreatedAt { get; set; }
}

[Table(TableName)]
public class Token
{
    public const string TableName = "token";

    [Key,StringLength(2048)] public required string Id { get; set; }
    [StringLength(2048)] public required string Did { get; set; }
    [StringLength(2048)] public required string TokenHash { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime ExpiresAt { get; set; }
}

[Table(TableName)]
public class RefreshToken
{
    public const string TableName = "refresh_token";

    [Key] [StringLength(2048)] public required string Id { get; set; }
    [StringLength(2048)] public required string Did { get; set; }
    public required DateTime ExpiresAt { get; set; }
    [StringLength(2048)] public string? AppPasswordName { get; set; }
    [StringLength(2048)] public string? NextId { get; set; }
}

[Table(TableName)]
public class UsedRefreshToken
{
    public const string TableName = "used_refresh_token";

    [Key,StringLength(2048)] public required string Id { get; set; }
    [StringLength(2048)] public required string Did { get; set; }
    public required DateTime UsedAt { get; set; }
}

[Table(TableName)]
public class Lexicon
{
    public const string TableName = "lexicon";

    [Key,StringLength(2048)] public required string Nsid { get; set; }
    [StringLength(2048)] public required string Uri { get; set; }
    [StringLength(2048)] public required string Cid { get; set; }
    public required string Def { get; set; }
}

[Table(TableName)]
public class AppPassword
{
    public const string TableName = "app_password";

    [StringLength(2048)] public required string Did { get; set; }
    [StringLength(2048)] public required string Name { get; set; }
    [StringLength(2048)] public required string PasswordSCrypt { get; set; }
    public required DateTime CreatedAt { get; set; }
    public bool Privileged { get; set; }
}

[Table(TableName)]
public class RepoRoot
{
    public const string TableName = "repo_root";

    [StringLength(2048)] public required string Did { get; set; }
    [StringLength(2048)] public required string Cid { get; set; }
    [StringLength(2048)] public required string Rev { get; set; }
    public DateTime IndexedAt { get; set; }
}

[Table(TableName)]
public class InviteCode
{
    public const string TableName = "invite_code";

    [StringLength(2048)] public required string Code { get; set; }
    public required int AvailableUses { get; set; }
    public bool Disabled { get; set; }
    [StringLength(2048)] public required string ForAccount { get; set; }
    [StringLength(2048)] public required string CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Table(TableName)]
public class InviteCodeUse
{
    public const string TableName = "invite_code_use";

    [Key] [StringLength(2048)] public required string Code { get; set; }
    [StringLength(2048)] public required string UsedBy { get; set; }
    public required DateTime UsedAt { get; set; }
}

[Table(TableName)]
public class EmailToken
{

    public enum EmailTokenPurpose
    {
        confirm_email,
        update_email,
        reset_password,
        delete_account,
        plc_operation
    }

    public const string TableName = "email_token";

    public EmailTokenPurpose Purpose { get; set; }
    [StringLength(2048)] public required string Did { get; set; }
    [StringLength(2048)] public required string Token { get; set; }
    public DateTime RequestedAt { get; set; }
}