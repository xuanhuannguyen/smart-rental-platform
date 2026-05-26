using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Administrative;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Route("api/administrative")]
public class AdministrativeController : ControllerBase
{
    private readonly IAdministrativeService administrativeService;

    public AdministrativeController(IAdministrativeService administrativeService)
    {
        this.administrativeService = administrativeService;
    }

    [HttpGet("provinces")]
    public async Task<IActionResult> GetProvinces(CancellationToken cancellationToken)
    {
        var result = await administrativeService.GetActiveProvincesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("provinces/{provinceCode}/wards")]
    public async Task<IActionResult> GetWards(string provinceCode, CancellationToken cancellationToken)
    {
        var result = await administrativeService.GetWardsByProvinceAsync(provinceCode, cancellationToken);
        return Ok(result);
    }
}
