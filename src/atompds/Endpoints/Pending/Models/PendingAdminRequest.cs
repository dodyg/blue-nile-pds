namespace atompds.Endpoints.Pending.Models;

public class PendingApproveRequest
{
    public int Id { get; set; }
}

public class PendingRejectRequest
{
    public int Id { get; set; }
    public string? Reason { get; set; }
}
