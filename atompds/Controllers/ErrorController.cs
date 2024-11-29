using atompds.Model;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

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
        
        if (exception is ErrorDetailException errorDetailException)
        {
            return StatusCode(errorDetailException.StatusCode, errorDetailException.ErrorDetail);
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