using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using CarpaNet;
using ComAtproto.Admin;
using ComAtproto.Server;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Admin;

public static class GetAccountInfoEndpoints
{
    public static RouteGroupBuilder MapGetAccountInfoEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("com.atproto.admin.getAccountInfo", HandleAsync).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(string? did, AccountRepository accountRepository, InviteStore inviteStore)
    {
        if (string.IsNullOrWhiteSpace(did))
            throw new XRPCError(new InvalidRequestErrorDetail("did is required"));
        var account = await accountRepository.GetAccountAsync(did, new AvailabilityFlags(true, true));
        if (account == null)
            throw new XRPCError(new InvalidRequestErrorDetail("Account not found"));

        var invitedByCodes = await inviteStore.GetInvitedByAsync(did);
        DefsInviteCode? invitedBy = null;
        if (invitedByCodes.Count > 0)
        {
            var ic = invitedByCodes[0];
            var uses = await inviteStore.GetInviteCodeUsesAsync([ic.Code]);
            invitedBy = MapInviteCode(ic, uses.GetValueOrDefault(ic.Code, []));
        }

        var accountInviteCodes = await inviteStore.GetInviteCodesForAccountAsync(did);
        var allCodeStrings = accountInviteCodes.Select(c => c.Code).ToList();
        var allUses = await inviteStore.GetInviteCodeUsesAsync(allCodeStrings);
        var invites = accountInviteCodes
            .Select(c => MapInviteCode(c, allUses.GetValueOrDefault(c.Code, [])))
            .ToList();

        return Results.Ok(new DefsAccountView
        {
            Did = new ATDid(account.Did),
            Handle = new ATHandle(account.Handle ?? string.Empty),
            Email = account.Email,
            EmailConfirmedAt = account.EmailConfirmedAt.HasValue
                ? new DateTimeOffset(account.EmailConfirmedAt.Value, TimeSpan.Zero) : null,
            InvitesDisabled = account.InvitesDisabled,
            IndexedAt = new DateTimeOffset(account.CreatedAt, TimeSpan.Zero),
            DeactivatedAt = account.DeactivatedAt.HasValue
                ? new DateTimeOffset(account.DeactivatedAt.Value, TimeSpan.Zero) : null,
            InvitedBy = invitedBy,
            Invites = invites.Count > 0 ? invites : null,
            RelatedRecords = null,
            InviteNote = null,
            ThreatSignatures = null
        });
    }

    private static DefsInviteCode MapInviteCode(InviteCode code, List<InviteCodeUse> uses)
    {
        return new DefsInviteCode
        {
            Code = code.Code,
            Available = code.AvailableUses,
            Disabled = code.Disabled,
            ForAccount = code.ForAccount,
            CreatedBy = code.CreatedBy,
            CreatedAt = new DateTimeOffset(code.CreatedAt, TimeSpan.Zero),
            Uses = uses.Select(u => new DefsInviteCodeUse
            {
                UsedBy = new ATDid(u.UsedBy),
                UsedAt = new DateTimeOffset(u.UsedAt, TimeSpan.Zero)
            }).ToList()
        };
    }
}
