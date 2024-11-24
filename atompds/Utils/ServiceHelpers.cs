using atompds.Database;
using Microsoft.EntityFrameworkCore;

namespace atompds.Utils;

public static class ServiceHelpers
{
    public static void AddDatabase(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<DataContext>(options =>
        {
            var connectionString = builder.Configuration.GetConnectionString("Default");
            options.UseSqlite(connectionString);
        });
    }
    
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<AccountRepository>();
        services.AddScoped<ConfigRepository>();
        return services;
    }
}