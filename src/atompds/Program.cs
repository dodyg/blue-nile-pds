using System.Text.Json.Serialization;
using System.Threading.Channels;
using AccountManager.Db;
using atompds.Config;
using atompds.Endpoints;
using atompds.ExceptionHandler;
using atompds.Middleware;
using atompds.Services;
using Config;
using Microsoft.AspNetCore.HttpLogging;
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

        builder.Services.AddHttpLogging(logging =>
        {
            logging.LoggingFields = HttpLoggingFields.RequestPath | HttpLoggingFields.ResponseStatusCode |
                                    HttpLoggingFields.RequestMethod;
            logging.CombineLogs = true;
        });

        builder.Services.AddExceptionHandler<XRPCExceptionHandler>();

        builder.Services.AddPdsRateLimiting(environment.PDS_RATE_LIMITS_ENABLED);

        // Background job queue
        builder.Services.AddSingleton<BackgroundJobQueue>();
        builder.Services.AddSingleton<IBackgroundJobQueue>(sp => sp.GetRequiredService<BackgroundJobQueue>());
        builder.Services.AddSingleton<ChannelWriter<Func<IServiceProvider, Task>>>(sp => sp.GetRequiredService<BackgroundJobQueue>().Writer);
        builder.Services.AddSingleton<BackgroundEmailDispatcher>();
        builder.Services.AddHostedService<BackgroundJobWorker>();


        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var accountManager = scope.ServiceProvider.GetRequiredService<AccountManagerDb>();
            await accountManager.Database.MigrateAsync();

            var seqDb = scope.ServiceProvider.GetRequiredService<SequencerDb>();
            await seqDb.Database.MigrateAsync();
        }

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

        app.UseCors(cors => cors.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

        if (app.Environment.IsDevelopment())
        {
            //app.UseHttpLogging();
        }

        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        await app.RunAsync();
    }
}
