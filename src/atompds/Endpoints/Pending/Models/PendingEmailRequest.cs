namespace atompds.Endpoints.Pending.Models;

public class PendingConfirmEmailRequest
{
    public string Token { get; set; } = "";
}

public class PendingUpdateEmailRequest
{
    public string Email { get; set; } = "";
}
