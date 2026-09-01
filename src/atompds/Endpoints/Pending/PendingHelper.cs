namespace atompds.Endpoints.Pending;

public static class PendingHelper
{
    public static int? GetPendingRegistrationId(HttpContext context)
    {
        if (context.Items.TryGetValue("PendingRegistrationId", out var item) && item is int id)
            return id;
        return null;
    }
}
