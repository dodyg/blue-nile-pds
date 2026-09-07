namespace BlueNilePds.Host.Endpoints.Pending.Models;

public class PendingConfirmEmailRequest
{
    public string Token { get; set; } = "";
}

public class PendingUpdateEmailRequest
{
    public string Email { get; set; } = "";
    public string? Token { get; set; }
}
