using System.Security.Claims;

namespace atompds;

public class JwtMiddleware : IMiddleware
{
    private readonly JwtHandler _jwtHandler;
    private readonly ILogger<JwtMiddleware> _logger;

    public JwtMiddleware(JwtHandler jwtHandler, ILogger<JwtMiddleware> logger)
    {
        _jwtHandler = jwtHandler;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
        if (token != null)
        {
            try
            {
                var identity = await _jwtHandler.ValidateJwtToken(token);
                context.User = new ClaimsPrincipal(identity);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to validate JWT token");
            }
        }
        
        await next(context);
    }
}