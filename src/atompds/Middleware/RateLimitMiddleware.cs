using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace atompds.Middleware;

public static class RateLimitExtensions
{
    public static IServiceCollection AddPdsRateLimiting(this IServiceCollection services, bool enabled)
    {
        if (!enabled)
        {
            return services;
        }

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await context.HttpContext.Response.WriteAsync("Rate limit exceeded", ct);
            };

            options.AddSlidingWindowLimiter("per-ip-global", opt =>
            {
                opt.PermitLimit = 500;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.SegmentsPerWindow = 4;
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 0;
            });

            options.AddSlidingWindowLimiter("auth-sensitive", opt =>
            {
                opt.PermitLimit = 30;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.SegmentsPerWindow = 4;
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 0;
            });

            options.AddSlidingWindowLimiter("repo-write", opt =>
            {
                opt.PermitLimit = 100;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.SegmentsPerWindow = 4;
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 0;
            });
        });

        return services;
    }
}
