using atompds.Config;
using atompds.Endpoints.OAuth;
using atompds.Endpoints.Xrpc;
using atompds.Endpoints.Xrpc.Com.Atproto.Admin;
using atompds.Endpoints.Xrpc.Com.Atproto.Identity;
using atompds.Endpoints.Xrpc.Com.Atproto.Moderation;
using atompds.Endpoints.Xrpc.Com.Atproto.Repo;
using atompds.Endpoints.Xrpc.Com.Atproto.Server;
using atompds.Endpoints.Xrpc.Com.Atproto.Sync;
using atompds.Endpoints.Xrpc.Com.Atproto.Temp;
using Config;

namespace atompds.Endpoints;

public static class EndpointRegistration
{
    public static WebApplication MapEndpoints(
        this WebApplication app,
        ServerEnvironment environment,
        ServiceConfig serviceConfig,
        IdentityConfig identityConfig)
    {
        app.MapRootEndpoints(environment, serviceConfig, identityConfig);
        app.MapErrorEndpoints();
        app.MapWellKnownEndpoints();

        app.MapOAuthTokenEndpoints();
        app.MapOAuthAuthorizeEndpoints();
        app.MapOAuthClientMetadataEndpoints();

        var xrpc = app.MapGroup("xrpc");
        xrpc.MapHealthEndpoints();

        var admin = xrpc.MapGroup("").WithTags("Admin");
        admin.MapAccountInvitesAdminEndpoints();
        admin.MapAdminDeleteAccountEndpoints();
        admin.MapDisableInviteCodesAdminEndpoints();
        admin.MapGetAccountInfoEndpoints();
        admin.MapGetAccountInfosEndpoints();
        admin.MapGetInviteCodesAdminEndpoints();
        admin.MapSendEmailAdminEndpoints();
        admin.MapSubjectStatusEndpoints();
        admin.MapUpdateAccountEmailAdminEndpoints();
        admin.MapUpdateAccountHandleAdminEndpoints();
        admin.MapUpdateAccountPasswordAdminEndpoints();

        var identity = xrpc.MapGroup("").WithTags("Identity");
        identity.MapResolveHandleEndpoints();
        identity.MapUpdateHandleEndpoints();
        identity.MapGetRecommendedDidCredentialsEndpoints();
        identity.MapRequestPlcOperationSignatureEndpoints();
        identity.MapSignPlcOperationEndpoints();
        identity.MapSubmitPlcOperationEndpoints();

        xrpc.MapCreateReportEndpoints();

        var repo = xrpc.MapGroup("").WithTags("Repo");
        repo.MapApplyWritesEndpoints();
        repo.MapListRecordsEndpoints();
        repo.MapDescribeRepoEndpoints();
        repo.MapBlobEndpoints();
        repo.MapImportRepoEndpoints();
        repo.MapListMissingBlobsEndpoints();

        var server = xrpc.MapGroup("").WithTags("Server");
        server.MapCreateAccountEndpoints();
        server.MapCreateSessionEndpoints();
        server.MapGetSessionEndpoints();
        server.MapRefreshSessionEndpoints();
        server.MapDeleteSessionEndpoints();
        server.MapDeleteAccountEndpoints();
        server.MapDescribeServerEndpoints();
        server.MapCreateInviteCodeEndpoints();
        server.MapCreateInviteCodesEndpoints();
        server.MapGetAccountInviteCodesEndpoints();
        server.MapCheckAccountStatusEndpoints();
        server.MapActivateAccountEndpoints();
        server.MapDeactivateAccountEndpoints();
        server.MapCreateAppPasswordEndpoints();
        server.MapListAppPasswordsEndpoints();
        server.MapRevokeAppPasswordEndpoints();
        server.MapUpdateEmailEndpoints();
        server.MapConfirmEmailEndpoints();
        server.MapRequestEmailConfirmationEndpoints();
        server.MapRequestEmailUpdateEndpoints();
        server.MapRequestPasswordResetEndpoints();
        server.MapResetPasswordEndpoints();
        server.MapReserveSigningKeyEndpoints();
        server.MapGetServiceAuthEndpoints();

        var sync = xrpc.MapGroup("").WithTags("Sync");
        sync.MapSubscribeReposEndpoints();
        sync.MapListReposEndpoints();
        sync.MapGetRepoEndpoints();
        sync.MapGetBlocksEndpoints();
        sync.MapGetBlobEndpoints();
        sync.MapListBlobsEndpoints();
        sync.MapGetRecordEndpoints();
        sync.MapGetLatestCommitEndpoints();
        sync.MapGetRepoStatusEndpoints();

        xrpc.MapCheckSignupQueueEndpoints();

        // AppViewProxy must be registered LAST — its catchall {nsid} must not override specific routes
        xrpc.MapAppViewProxyEndpoints();

        return app;
    }
}
