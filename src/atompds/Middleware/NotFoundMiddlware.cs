namespace atompds.Middleware;

public static class NotFoundMiddlewareExtensions
{
    public static IApplicationBuilder UseNotFoundMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<NotFoundMiddleware>();
    }
}

public class NotFoundMiddleware
{
    private readonly RequestDelegate _next;
    public NotFoundMiddleware(RequestDelegate next)
    {
        _next = next;
    }
    
    public async Task Invoke(HttpContext context)
    {
        await _next(context);
        if (context.Response.StatusCode == 404)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<NotFoundMiddleware>>();
            logger.LogWarning("404 Not Found: {Path}", context.Request.Path);
        }
    }
}