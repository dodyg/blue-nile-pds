using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using atompds.Auth;
using atompds.Controllers;
using atompds.Database;
using atompds.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

namespace atompds;

public class Program
{
	public const string FixedWindowLimiterName = "fixed-window";
	
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        builder.Services.AddOpenApi();
        builder.Services.AddCors();
        builder.AddDatabase();
        builder.Services.AddRepositories();
        builder.Services.Configure<CliConfig>(builder.Configuration.GetSection("Config"));
        
        // response serialize, ignore when writing default
        builder.Services.AddControllers().AddJsonOptions(options =>
		{
	        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
		});
        
        builder.Services.AddRateLimiter(r => r
	        .AddFixedWindowLimiter(FixedWindowLimiterName, options =>
	        {
		        // 4 requests per second, oldest first, queue limit of 2
		        options.PermitLimit = 4;
		        options.Window = TimeSpan.FromSeconds(10);
		        options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
		        options.QueueLimit = 2;
	        }));
        
        builder.Services.AddHttpLogging(logging =>
        {
	        logging.LoggingFields = HttpLoggingFields.RequestPath | HttpLoggingFields.ResponseStatusCode |
	                                HttpLoggingFields.RequestMethod;
			logging.CombineLogs = true;
		});
        
		builder.Services.ConfigureAuthServices();
		builder.Services.AddScoped<DataContextSeeder>();
		
        var app = builder.Build();
        
        using (var scope = app.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<DataContext>();
			await db.SetupAsync();
			var seeder = scope.ServiceProvider.GetRequiredService<DataContextSeeder>();
			await seeder.SeedAsync();
		}
        
        app.UseRouting();
        app.ConfigureAuthApp();
        app.MapControllers();
        app.UseExceptionHandler("/error");	
        
        app.UseRateLimiter();
        app.UseCors(cors => cors.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        
        if (app.Environment.IsDevelopment())
        {
	        app.MapOpenApi();
	        app.UseHttpLogging();
        }

        var version = typeof(Program).Assembly.GetName().Version!.ToString(3);
        app.MapGet("/", () => $"Hello! This is an ATProto PDS instance, running atompds v{version}.");
        await app.RunAsync();
    }
}

public class CliConfig
{
    public CliConfig() { }

	public CliConfig(string pdsPfx, string pdsDid, string[] availableUserDomains)
	{
		PdsPfx = pdsPfx;
		PdsDid = pdsDid;
		AvailableUserDomains = availableUserDomains;
	}

	public string PdsPfx { get; init; } = default!;
	public string PdsDid { get; init; } = default!;
	public string AppViewUrl { get; init; } = default!;
	public string AppViewDid { get; init; } = default!;
	public string[] AvailableUserDomains { get; init; } = [];
}