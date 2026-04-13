using Microsoft.AspNetCore.Diagnostics;
using Xrpc;

namespace atompds.Endpoints;

public static class ErrorEndpoints
{
    public static WebApplication MapErrorEndpoints(this WebApplication app)
    {
        app.MapGet("/error", Handle);
        app.MapPost("/error", Handle);
        return app;
    }

    private static IResult Handle(HttpContext context, ILogger<Program> logger)
    {
        var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        if (exceptionHandlerPathFeature == null)
        {
            return Results.Problem("You're not supposed to be here", statusCode: 400);
        }

        var exception = exceptionHandlerPathFeature.Error;

        if (exception is XRPCError errorDetailException)
        {
            return Results.Json(errorDetailException.Detail, statusCode: (int)errorDetailException.Status);
        }

        var guid = Guid.NewGuid().ToString();
        logger.LogError(exception, "Unhandled exception: {Guid}", guid);

        return Results.Problem($"An error occurred. Reference ID: {guid}", title: "Internal Server Error", statusCode: 500);
    }
}
