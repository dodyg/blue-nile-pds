using atompds.ErrorDetail;
using Microsoft.AspNetCore.Mvc.Filters;

namespace atompds.Auth;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class JwtAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (!user.Claims.Any())
        {
            throw new ErrorDetailException(new InvalidTokenErrorDetail("Invalid Token"), 401);
        }
    }
} 