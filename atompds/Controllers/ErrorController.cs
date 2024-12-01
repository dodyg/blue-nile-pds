using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Xrpc;

namespace atompds.Controllers;

[ApiController]
[Route("[controller]")]
public class ErrorController : ControllerBase
{
    private readonly ILogger<ErrorController> _logger;

    public ErrorController(ILogger<ErrorController> logger)
    {
        _logger = logger;
    }
    
    // problemdetails wrapper for error responses
    [HttpGet]
    public IActionResult Error()
    {
        var exceptionHandlerPathFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        if (exceptionHandlerPathFeature == null)
        {
            return Problem(
                detail: "You're not supposed to be here",
                statusCode: 400
            );
        }
        
        var exception = exceptionHandlerPathFeature.Error;
        
        if (exception is XRPCError errorDetailException)
        {
            return StatusCode((int)errorDetailException.Status, errorDetailException.Detail);
        }
        
        var guid = Guid.NewGuid().ToString();
        _logger.LogError(exception, "Unhandled exception: {Guid}", guid);
        
        return Problem(
            detail: $"An error occurred. Reference ID: {guid}",
            title: "Internal Server Error",
            statusCode: 500
        );
    }
}