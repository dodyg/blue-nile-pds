using System.Text.Json.Serialization;
using AccountManager.Db;
using atompds.Config;
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
        builder.Services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
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
        app.UseRateLimiter();
        app.MapControllers();
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
        var version = typeof(Program).Assembly.GetName().Version!.ToString(3);
        var serviceConfig = app.Services.GetRequiredService<ServiceConfig>();
        app.MapGet("/", () => Results.Json(new
        {
            did = serviceConfig.Did,
            version,
            availableUserDomains = serviceConfig.Hostname
        }));

        app.MapGet("/robots.txt", () => "User-agent: *\nAllow: /xrpc/\nDisallow: /");

        app.MapGet("/tls-check", (HttpContext ctx) =>
        {
            var proto = ctx.Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? ctx.Request.Scheme;
            return Results.Ok(new { proto, host = ctx.Request.Host.Host });
        });

        await app.RunAsync();
    }
}
