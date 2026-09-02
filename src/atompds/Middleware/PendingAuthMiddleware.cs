using Microsoft.AspNetCore.Authorization;
using PendingAccounts;
using PendingAccounts.Services;

namespace BlueNilePds.Middleware;

public class PendingAuthMiddleware
{
    private readonly RequestDelegate _next;

    public PendingAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint == null)
        {
            await _next(context);
            return;
        }

        var pendingAuth = endpoint.Metadata.GetMetadata<PendingAccessAttribute>();
        if (pendingAuth != null)
        {
            var jwtService = context.RequestServices.GetRequiredService<PendingJwtService>();
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (authHeader != null && authHeader.StartsWith("Bearer "))
            {
                var token = authHeader["Bearer ".Length..];
                var data = jwtService.ValidateAccessToken(token);
                if (data != null)
                {
                    context.Items["PendingRegistrationId"] = data.PendingRegistrationId;
                }
                else
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsJsonAsync(new { error = "Invalid or expired token" });
                    return;
                }
            }
            else
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "Authorization required" });
                return;
            }
        }

        await _next(context);
    }
}

public class PendingAccessAttribute : Attribute { }

public static class PendingAuthMiddlewareExtensions
{
    public static IApplicationBuilder UsePendingAuth(this IApplicationBuilder app)
    {
        return app.UseMiddleware<PendingAuthMiddleware>();
    }
}
