using BlueNilePds.Core.Did;
using BlueNilePds.Core.Handle;
using BlueNilePds.Core.Identity;
using BlueNilePds.Host.Middleware;
using BlueNilePds.Pds.AccountManager;
using BlueNilePds.Pds.AccountManager.Db;
using BlueNilePds.Pds.Config;
using BlueNilePds.Pds.Sequencer;
using BlueNilePds.Pds.Xrpc;
using ComAtproto.Admin;

namespace BlueNilePds.Host.Endpoints.Xrpc.Com.Atproto.Admin;

public static class UpdateAccountHandleAdminEndpoints
{
    public static RouteGroupBuilder MapUpdateAccountHandleAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.admin.updateAccountHandle", HandleAsync).WithMetadata(new AdminTokenAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        UpdateAccountHandleInput request,
        AccountRepository accountRepository,
        HandleManager handleManager,
        SequencerRepository sequencer,
        PlcClient plcClient,
        SecretsConfig secretsConfig,
        ServiceConfig serviceConfig,
        ILogger<Program> logger,
        IDidCache didCache)
    {
        var did = (string)request.Did;
        if (string.IsNullOrWhiteSpace(did) || string.IsNullOrWhiteSpace(request.Handle))
            throw new XRPCError(new InvalidRequestErrorDetail("did and handle are required"));

        var handle = await handleManager.NormalizeAndValidateHandleAsync(request.Handle, did, false);

        var existingAccount = await accountRepository.GetAccountAsync(handle, new AvailabilityFlags(true, true));
        if (existingAccount != null && existingAccount.Did != did)
        {
            throw new XRPCError(new HandleNotAvailableErrorDetail($"Handle already taken: {handle}"));
        }

        try
        {
            var signingKeyDid = secretsConfig.PlcRotationKey.Did();
            var prevCid = await plcClient.GetLastOperationCidAsync(did);
            var op = await Operations.AtProtoOpAsync(
                signingKeyDid,
                handle,
                serviceConfig.PublicUrl,
                [secretsConfig.PlcRotationKey.Did()],
                prevCid,
                secretsConfig.PlcRotationKey);
            await plcClient.SendOperationAsync(did, op);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update PLC handle for {did}", did);
            throw new XRPCError(new InvalidRequestErrorDetail("Failed to update PLC handle"), e);
        }

        await accountRepository.UpdateHandleAsync(did, handle);
        await sequencer.SequenceIdentityEventAsync(did, handle);

        try
        {
            await didCache.ClearEntryAsync(did);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clear DID cache for {Did}", did);
        }

        return Results.Ok(new { });
    }
}
