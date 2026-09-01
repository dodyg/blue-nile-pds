using System.ComponentModel.DataAnnotations;

namespace PendingAccounts.Models;

public enum PendingEmailTokenPurpose
{
    confirm_email,
    update_email
}

public class PendingEmailToken
{
    [Key]
    public int Id { get; set; }

    public int PendingRegistrationId { get; set; }

    public PendingEmailTokenPurpose Purpose { get; set; }

    [Required]
    [StringLength(2048)]
    public required string Token { get; set; }

    [StringLength(2048)]
    public string? NewEmail { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
}
