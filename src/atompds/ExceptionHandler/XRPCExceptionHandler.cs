using System;
using Microsoft.AspNetCore.Diagnostics;
using Xrpc;

namespace atompds.ExceptionHandler;

// https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0#iexceptionhandler
public class XRPCExceptionHandler(
    ILogger<XRPCExceptionHandler> logger
) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is XRPCError xrpcError)
        {
            logger.LogInformation(exception, "Handled XRPCError: \nerror: {Error}\nmessage: {Message}", xrpcError.Error, xrpcError.Message);
            httpContext.Response.StatusCode = (int) xrpcError.Status;
            httpContext.Response.ContentType = "application/json";

            var errorResponse = new
            {
                error = xrpcError.Error,
                message = xrpcError.Message
            };
            
            await httpContext.Response.WriteAsJsonAsync(errorResponse, cancellationToken);
            return true;
        }

        return false;
    }
}
