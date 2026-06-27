using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace atompds.Middleware;

public static class RateLimitExtensions
{
    public static IServiceCollection AddPdsRateLimiting(this IServiceCollection services, bool enabled, string? bypassKey, List<string> bypassIps)
    {
        if (!enabled)
        {
            return services;
        }

        var bypassNetworks = bypassIps
            .Select(ParseCidr)
            .OfType<(IPAddress Network, int PrefixLength)>()
            .ToList();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await context.HttpContext.Response.WriteAsync("Rate limit exceeded", ct);
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (ShouldBypass(context, bypassKey, bypassNetworks))
                    return RateLimitPartition.GetNoLimiter("bypass");
                return RateLimitPartition.GetSlidingWindowLimiter(GetClientIpPartition(context), _ => CreateSlidingWindowOptions(500));
            });

            options.AddPolicy("auth-sensitive", context =>
            {
                if (ShouldBypass(context, bypassKey, bypassNetworks))
                    return RateLimitPartition.GetNoLimiter("bypass");
                return RateLimitPartition.GetSlidingWindowLimiter(GetClientIpPartition(context), _ => CreateSlidingWindowOptions(30));
            });

            options.AddPolicy("repo-write", context =>
            {
                if (ShouldBypass(context, bypassKey, bypassNetworks))
                    return RateLimitPartition.GetNoLimiter("bypass");
                return RateLimitPartition.GetSlidingWindowLimiter(GetClientIpPartition(context), _ => CreateSlidingWindowOptions(100));
            });
        });

        return services;
    }

    private static bool ShouldBypass(HttpContext context, string? bypassKey, List<(IPAddress Network, int PrefixLength)> bypassNetworks)
    {
        if (!string.IsNullOrWhiteSpace(bypassKey))
        {
            var header = context.Request.Headers["x-ratelimit-bypass"].FirstOrDefault();
            if (header == bypassKey)
                return true;
        }

        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp != null)
        {
            foreach (var (network, prefix) in bypassNetworks)
            {
                if (IsInNetwork(remoteIp, network, prefix))
                    return true;
            }
        }

        return false;
    }

    private static (IPAddress Network, int PrefixLength)? ParseCidr(string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var network) || !int.TryParse(parts[1], out var prefix))
            return null;
        return (network, prefix);
    }

    private static bool IsInNetwork(IPAddress address, IPAddress network, int prefixLength)
    {
        var addrBytes = address.GetAddressBytes();
        var netBytes = network.GetAddressBytes();
        if (addrBytes.Length != netBytes.Length)
            return false;

        int bits = prefixLength;
        for (int i = 0; i < addrBytes.Length && bits > 0; i++)
        {
            int mask = bits >= 8 ? 0xFF : (0xFF << (8 - bits)) & 0xFF;
            if ((addrBytes[i] & mask) != (netBytes[i] & mask))
                return false;
            bits -= 8;
        }
        return true;
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
