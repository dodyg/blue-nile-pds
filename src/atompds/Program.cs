using System.Text.Json.Serialization;
using AccountManager.Db;
using atompds.Config;
using atompds.Endpoints;
using atompds.ExceptionHandler;
using atompds.Middleware;
using atompds.Services;
using Config;
using Microsoft.EntityFrameworkCore;
using Sequencer.Db;

namespace atompds;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        builder.Services.AddCors();
        builder.Services.AddHttpClient();

        // validate server environment
        var environment = builder.Configuration.GetSection("Config").Get<ServerEnvironment>() ?? throw new Exception("Missing server environment configuration");
        var serverConfig = new ServerConfig(environment);

        ServerConfig.RegisterServices(builder.Services, serverConfig);

        // response serialize, ignore when writing default
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
        });


        builder.Services.AddExceptionHandler<XRPCExceptionHandler>();

        builder.Services.AddPdsRateLimiting(environment.PDS_RATE_LIMITS_ENABLED);

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var accountManager = scope.ServiceProvider.GetRequiredService<AccountManagerDb>();
            await accountManager.Database.MigrateAsync();

            var seqDb = scope.ServiceProvider.GetRequiredService<SequencerDb>();
            await seqDb.Database.MigrateAsync();
        }

        app.UseCors(cors => cors.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        app.UseRouting();
        if (environment.PDS_RATE_LIMITS_ENABLED)
        {
            app.UseRateLimiter();
        }
        app.MapEndpoints(
            environment,
            app.Services.GetRequiredService<ServiceConfig>(),
            app.Services.GetRequiredService<IdentityConfig>());
        app.UseExceptionHandler("/error");
        app.UseAuthMiddleware();
        app.UseNotFoundMiddleware();
        app.UseWebSockets();


        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        await app.RunAsync();
    }
}
