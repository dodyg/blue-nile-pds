namespace BlueNilePds.Host.Endpoints.Pending.Models;

public class PendingRegisterRequest
{
    public string Email { get; set; } = "";
    public string Handle { get; set; } = "";
    public string Password { get; set; } = "";
    public string? InviteCode { get; set; }
    public string? Location { get; set; }
    public string? AccountType { get; set; }
}

public class PendingLoginRequest
{
    public string Identifier { get; set; } = "";
    public string Password { get; set; } = "";
}

public class PendingRefreshRequest
{
    public string RefreshJwt { get; set; } = "";
}
