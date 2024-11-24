using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace atompds.Auth;

public static class AuthenticationStartup
{
    public static void ConfigureAuthServices(this IServiceCollection services)
    {
        services.AddScoped<JwtHandler>();
        services.AddTransient<JwtMiddleware>();
        services.AddAuthentication(opt =>
        {
            opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        });
    }
    
    public static void ConfigureAuthApp(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<JwtMiddleware>();
    }
}