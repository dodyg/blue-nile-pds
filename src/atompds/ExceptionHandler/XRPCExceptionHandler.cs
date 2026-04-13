using System.Text.Json;
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

        if (exception is JsonException jsonEx)
        {
            logger.LogInformation(jsonEx, "JSON deserialization error");
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsJsonAsync(new { error = "InvalidRequest", message = jsonEx.Message }, cancellationToken);
            return true;
        }

        if (exception is BadHttpRequestException badRequest)
        {
            logger.LogInformation(badRequest, "Bad HTTP request");
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsJsonAsync(new { error = "InvalidRequest", message = badRequest.Message }, cancellationToken);
            return true;
        }

        return false;
    }
}
