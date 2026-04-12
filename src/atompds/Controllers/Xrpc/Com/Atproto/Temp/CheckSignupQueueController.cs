using Microsoft.AspNetCore.Mvc;

namespace atompds.Controllers.Xrpc.Com.Atproto.Temp;

[ApiController]
[Route("xrpc")]
public class CheckSignupQueueController : ControllerBase
{
    [HttpGet("com.atproto.temp.checkSignupQueue")]
    public IActionResult CheckSignupQueue()
    {
        return Ok(new
        {
            activated = true,
            placeInQueue = 0
        });
    }
}
