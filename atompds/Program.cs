using System.Text.Json.Serialization;
using AccountManager.Db;
using atompds.Middleware;
using atompds.Pds.Config;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using Sequencer;
using Sequencer.Db;

namespace atompds;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        builder.Services.AddOpenApi();
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
        

        var app = builder.Build();
        
        using (var scope = app.Services.CreateScope())
		{
			var accountManager = scope.ServiceProvider.GetRequiredService<AccountManagerDb>();
			await accountManager.Database.MigrateAsync();
			
			var seqDb = scope.ServiceProvider.GetRequiredService<SequencerDb>();
			await seqDb.Database.MigrateAsync();
			
			var sequencerRepo = scope.ServiceProvider.GetRequiredService<SequencerRepository>();
		}

        app.UseRouting();
        app.MapControllers();
        app.UseExceptionHandler("/error");	
        app.UseAuthMiddleware();
        app.UseWebSockets();
        
        app.UseCors(cors => cors.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        
        if (app.Environment.IsDevelopment())
        {
	        app.MapOpenApi();
	        //app.UseHttpLogging();
        }

        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        var version = typeof(Program).Assembly.GetName().Version!.ToString(3);
        app.MapGet("/", () => $"Hello! This is an ATProto PDS instance, running atompds v{version}.");
        app.MapFallback(context =>
		{
			logger.LogWarning("Unhandled request: {Path}", context.Request.Path);
			return Task.CompletedTask;
		});
        await app.RunAsync();
    }
}