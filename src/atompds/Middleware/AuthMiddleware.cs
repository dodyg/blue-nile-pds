using Xrpc;

namespace atompds.Middleware;

public class AuthMiddleware
{
    private readonly RequestDelegate _next;

    public AuthMiddleware(RequestDelegate next)
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

        var verifier = context.RequestServices.GetRequiredService<AuthVerifier>();
        var adminToken = endpoint.Metadata.GetMetadata<AdminTokenAttribute>();
        if (adminToken != null)
        {
            var output = await adminToken.HandleAsync(verifier, context);
            context.Items["AuthOutput"] = output;
        }

        var accessStandard = endpoint.Metadata.GetMetadata<AccessStandardAttribute>();
        if (accessStandard != null)
        {
            var output = await accessStandard.HandleAsync(verifier, context);
            context.Items["AuthOutput"] = output;
        }

        var accessFull = endpoint.Metadata.GetMetadata<AccessFullAttribute>();
        if (accessFull != null)
        {
            var output = await accessFull.HandleAsync(verifier, context);
            context.Items["AuthOutput"] = output;
        }

        var accessPrivileged = endpoint.Metadata.GetMetadata<AccessPrivilegedAttribute>();
        if (accessPrivileged != null)
        {
            var output = await accessPrivileged.HandleAsync(verifier, context);
            context.Items["AuthOutput"] = output;
        }

        var refresh = endpoint.Metadata.GetMetadata<RefreshAttribute>();
        if (refresh != null)
        {
            var output = await refresh.HandleAsync(verifier, context);
            context.Items["AuthOutput"] = output;
        }

        await _next(context);
    }
}

public static class AuthMiddlewareExtensions
{
    public static IApplicationBuilder UseAuthMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuthMiddleware>();
    }

    public static AuthVerifier.AccessOutput GetAuthOutput(this HttpContext context)
    {
        if (!context.Items.TryGetValue("AuthOutput", out var item))
        {
            throw new XRPCError(new AuthRequiredErrorDetail("Auth Required"));
        }

        if (item is not AuthVerifier.AccessOutput output)
        {
            throw new XRPCError(new AuthRequiredErrorDetail("Auth Required"));
        }

        return output;
    }

    public static AuthVerifier.RefreshOutput GetRefreshOutput(this HttpContext context)
    {
        if (!context.Items.TryGetValue("AuthOutput", out var item))
        {
            throw new XRPCError(new AuthRequiredErrorDetail("Auth Required"));
        }

        if (item is not AuthVerifier.RefreshOutput output)
        {
            throw new XRPCError(new AuthRequiredErrorDetail("Auth Required"));
        }

        return output;
    }
}

public class AdminTokenAttribute : Attribute
{
    public Task<AuthVerifier.AdminOutput> HandleAsync(AuthVerifier verifier, HttpContext context)
    {
        return verifier.AdminTokenAsync(context);
    }
}

public class AccessStandardAttribute : Attribute
{
    private readonly bool _checkDeactivated;
    private readonly bool _checkTakenDown;

    public AccessStandardAttribute(bool checkTakenDown = false, bool checkDeactivated = false)
    {
        _checkTakenDown = checkTakenDown;
        _checkDeactivated = checkDeactivated;
    }

    public Task<AuthVerifier.AccessOutput> HandleAsync(AuthVerifier verifier, HttpContext context)
    {
        return verifier.AccessStandardAsync(context, _checkTakenDown, _checkDeactivated);
    }
}

public class AccessFullAttribute : Attribute
{
    private readonly bool _checkDeactivated;
    private readonly bool _checkTakenDown;

    public AccessFullAttribute(bool checkTakenDown = false, bool checkDeactivated = false)
    {
        _checkTakenDown = checkTakenDown;
        _checkDeactivated = checkDeactivated;
    }

    public Task<AuthVerifier.AccessOutput> HandleAsync(AuthVerifier verifier, HttpContext context)
    {
        return verifier.AccessFullAsync(context, _checkTakenDown, _checkDeactivated);
    }
}

public class AccessPrivilegedAttribute : Attribute
{
    private readonly bool _checkDeactivated;
    private readonly bool _checkTakenDown;

    public AccessPrivilegedAttribute(bool checkTakenDown = false, bool checkDeactivated = false)
    {
        _checkTakenDown = checkTakenDown;
        _checkDeactivated = checkDeactivated;
    }

    public Task<AuthVerifier.AccessOutput> HandleAsync(AuthVerifier verifier, HttpContext context)
    {
        return verifier.AccessPrivilegedAsync(context, _checkTakenDown, _checkDeactivated);
    }
}

public class RefreshAttribute : Attribute
{
    public Task<AuthVerifier.RefreshOutput> HandleAsync(AuthVerifier verifier, HttpContext context)
    {
        return Task.FromResult(verifier.Refresh(context));
    }
}
