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
    
    public virtual Actor Actor { get; set; }
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

// [Table(TableName)]
// public class Device
// {
//     public const string TableName = "device";
//
//     [Key,StringLength(2048)] public required string DeviceId { get; set; }
//     [StringLength(2048)] public required string SessionId { get; set; }
//     [StringLength(2048)] public string? UserAgent { get; set; }
//     [StringLength(2048)] public string? IpAddress { get; set; }
//     public required DateTime LastSeenAt { get; set; }
// }
//
// [Table(TableName)]
// public class DeviceAccount
// {
//     public const string TableName = "device_account";
//
//     [Key,StringLength(2048)] public required string Did { get; set; }
//     [StringLength(2048)] public required string DeviceId { get; set; }
//     public required DateTime AuthenticatedAt { get; set; }
//     public required string[] AuthorizedClients { get; set; }
//     public required bool Remember { get; set; }
// }
//
// [Table(TableName)]
// public class AuthorizationRequest
// {
//     public const string TableName = "authorization_request";
//
//     [Key,StringLength(2048)] public required string RequestId { get; set; }
//     [StringLength(2048)] public string? Did { get; set; }
//     [StringLength(2048)] public string? DeviceId { get; set; }
//     [StringLength(2048)] public required string OAuthClientId { get; set; }
//     [StringLength(2048)] public required string ClientAuth { get; set; }
//     [StringLength(2048)] public required string Parameters { get; set; }
//     public required DateTime ExpiresAt { get; set; }
//     [StringLength(2048)] public string? Code { get; set; }
// }
//
// [Table(TableName)]
// public class Token
// {
//     public const string TableName = "token";
//
//     [Key] public int Id { get; set; }
//     [StringLength(2048)] public required string Did { get; set; }
//     [StringLength(2048)] public required string TokenId { get; set; }
//     public required DateTime CreatedAt { get; set; }
//     public required DateTime UpdatedAt { get; set; }
//     public required DateTime ExpiresAt { get; set; }
//     [StringLength(2048)] public required string OAuthClientId { get; set; }
//     [StringLength(2048)] public required string ClientAuth { get; set; }
//     [StringLength(2048)] public string? DeviceId { get; set; }
//     [StringLength(2048)] public required string Parameters { get; set; }
//     [StringLength(2048)] public required string Details { get; set; }
//     [StringLength(2048)] public required string Code { get; set; }
//     [StringLength(2048)] public string? RefreshToken { get; set; }
// }

[Table(TableName)]
public class RefreshToken
{
    public const string TableName = "refresh_token";

    [Key,StringLength(2048)] public required string Id { get; set; }
    [StringLength(2048)] public required string Did { get; set; }
    public required DateTime ExpiresAt { get; set; }
    [StringLength(2048)] public string? AppPasswordName { get; set; }
    [StringLength(2048)] public string? NextId { get; set; }
}

// [Table(TableName)]
// public class UsedRefreshToken
// {
//     public const string TableName = "used_refresh_token";
//
//     [StringLength(2048)] public required string TokenId { get; set; }
//     [StringLength(2048)] public required string RefreshToken { get; set; }
// }

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

    [Key,StringLength(2048)] public required string Code { get; set; }
    [StringLength(2048)] public required string UsedBy { get; set; }
    public required DateTime UsedAt { get; set; }
}

// [Table(TableName)]
// public class EmailToken
// {
//     public const string TableName = "email_token";
//
//     public enum EmailTokenPurpose
//     {
//         confirm_email,
//         update_email,
//         reset_password,
//         delete_account,
//         plc_operation
//     }
//
//     public EmailTokenPurpose Purpose { get; set; }
//     [StringLength(2048)] public required string Did { get; set; }
//     [StringLength(2048)] public required string Token { get; set; }
//     public DateTime RequestedAt { get; set; }
// }