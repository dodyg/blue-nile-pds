using AccountManager;
using AccountManager.Db;
using atompds.Middleware;
using Config;
using DidLib;
using Handle;
using Sequencer;
using Xrpc;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Identity;

public static class UpdateHandleEndpoints
{
    public static RouteGroupBuilder MapUpdateHandleEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.identity.updateHandle", HandleAsync).WithMetadata(new AccessPrivilegedAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        UpdateHandleRequest request,
        AccountRepository accountRepository,
        HandleManager handleManager,
        PlcClient plcClient,
        SecretsConfig secretsConfig,
        ServiceConfig serviceConfig,
        SequencerRepository sequencer,
        ILogger<Program> logger)
    {
        var auth = context.GetAuthOutput();
        var did = auth.AccessCredentials.Did;

        if (string.IsNullOrWhiteSpace(request.Handle))
            throw new XRPCError(new InvalidRequestErrorDetail("handle is required"));

        var validatedHandle = await handleManager.NormalizeAndValidateHandleAsync(request.Handle, did, false);
        var existingAccount = await accountRepository.GetAccountAsync(validatedHandle, new AvailabilityFlags(true, true));
        if (existingAccount != null && existingAccount.Did != did)
        {
            throw new XRPCError(new HandleNotAvailableErrorDetail($"Handle already taken: {validatedHandle}"));
        }

        try
        {
            var signingKeyDid = secretsConfig.PlcRotationKey.Did();
            var op = await Operations.AtProtoOpAsync(
                signingKeyDid,
                validatedHandle,
                serviceConfig.PublicUrl,
                [secretsConfig.PlcRotationKey.Did()],
                null,
                secretsConfig.PlcRotationKey);
            await plcClient.SendOperationAsync(did, op);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update PLC handle for {did}", did);
            throw new XRPCError(new InvalidRequestErrorDetail("Failed to update PLC handle"), e);
        }

        await accountRepository.UpdateHandleAsync(did, validatedHandle);
        await sequencer.SequenceIdentityEventAsync(did, validatedHandle);

        return Results.Ok();
    }
}

public class UpdateHandleRequest
{
    public string? Handle { get; set; }
}
