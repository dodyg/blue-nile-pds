using System.ComponentModel.DataAnnotations;

namespace PendingAccounts.Models;

public class PendingProfile
{
    [Key]
    public int Id { get; set; }

    public int PendingRegistrationId { get; set; }
    public PendingRegistration PendingRegistration { get; set; } = null!;

    [StringLength(2048)]
    public string? DisplayName { get; set; }

    [StringLength(2048)]
    public string? Description { get; set; }

    [StringLength(2048)]
    public string? AvatarBlobRef { get; set; }

    [StringLength(2048)]
    public string? Location { get; set; }

    [StringLength(2048)]
    public string? AccountType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
