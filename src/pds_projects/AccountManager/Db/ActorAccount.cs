namespace AccountManager.Db;

public record ActorAccount(string Did,
    string? Handle,
    DateTime CreatedAt,
    string? TakedownRef,
    DateTime? DeactivatedAt,
    DateTime? DeleteAfter,
    string? Email,
    DateTime? EmailConfirmedAt,
    bool? InvitesDisabled)
{

    public bool SoftDeleted => TakedownRef != null;
    public static ActorAccount? From(Actor? actor, Account? account)
    {
        if (actor == null)
        {
            return null;
        }

        return new ActorAccount(actor.Did, actor.Handle, actor.CreatedAt, actor.TakedownRef, actor.DeactivatedAt,
            actor.DeleteAfter, account?.Email, account?.EmailConfirmedAt, account?.InvitesDisabled);
    }
}