using Microsoft.AspNetCore.Mvc;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "OK",
            service = "SmartRentalPlatform.Api",
            time = DateTime.UtcNow
        });
    }
}