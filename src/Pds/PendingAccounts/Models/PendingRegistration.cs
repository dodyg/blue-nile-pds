using System.ComponentModel.DataAnnotations;

namespace BlueNilePds.Pds.PendingAccounts.Models;

public enum PendingRegistrationStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public class PendingRegistration
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(2048)]
    public required string Email { get; set; }

    [Required]
    [StringLength(2048)]
    public required string Handle { get; set; }

    [Required]
    [StringLength(2048)]
    public required string PasswordScrypt { get; set; }

    [StringLength(2048)]
    public string? InviteCode { get; set; }

    public PendingRegistrationStatus Status { get; set; } = PendingRegistrationStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAt { get; set; }

    [StringLength(2048)]
    public string? RejectionReason { get; set; }

    public DateTime? EmailConfirmedAt { get; set; }
}
