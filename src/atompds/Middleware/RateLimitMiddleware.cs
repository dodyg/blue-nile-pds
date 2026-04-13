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

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetSlidingWindowLimiter(GetClientIpPartition(context), _ => CreateSlidingWindowOptions(500)));

            options.AddPolicy("auth-sensitive", context =>
                RateLimitPartition.GetSlidingWindowLimiter(GetClientIpPartition(context), _ => CreateSlidingWindowOptions(30)));

            options.AddPolicy("repo-write", context =>
                RateLimitPartition.GetSlidingWindowLimiter(GetClientIpPartition(context), _ => CreateSlidingWindowOptions(100)));
        });

        return services;
    }

    private static string GetClientIpPartition(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static SlidingWindowRateLimiterOptions CreateSlidingWindowOptions(int permitLimit) =>
        new()
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 4,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        };
}
