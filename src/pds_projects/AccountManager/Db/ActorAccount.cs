namespace AccountManager.Db;

public record ActorAccount(
    string Did,
    string? Handle,
    DateTime CreatedAt,
    string? TakedownRef,
    DateTime? DeactivatedAt,
    DateTime? DeleteAfter,
    DateTime? SuspendedAt,
    string? Email,
    DateTime? EmailConfirmedAt,
    bool? InvitesDisabled,
    string? Location,
    string? AccountType)
{

    public bool SoftDeleted => TakedownRef != null;
    public static ActorAccount? From(Actor? actor, Account? account)
    {
        if (actor == null)
        {
            return null;
        }

        return new ActorAccount(actor.Did, actor.Handle, actor.CreatedAt, actor.TakedownRef, actor.DeactivatedAt,
            actor.DeleteAfter, actor.SuspendedAt, account?.Email, account?.EmailConfirmedAt, account?.InvitesDisabled,
            account?.Location, account?.AccountType);
    }
}