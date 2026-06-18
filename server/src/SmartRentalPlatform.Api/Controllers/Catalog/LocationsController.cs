using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Locations;

namespace SmartRentalPlatform.Api.Controllers.Catalog;

[ApiController]
[Route("api/locations")]
public class LocationsController : ControllerBase
{
    private readonly IVietMapService vietMapService;

    public LocationsController(IVietMapService vietMapService)
    {
        this.vietMapService = vietMapService;
    }

    [AllowAnonymous]
    [HttpGet("search-address")]
    public async Task<ActionResult<ApiResponse<LocationSearchResponse>>> SearchAddress(
        [FromQuery] string text,
        CancellationToken cancellationToken)
    {
        var result = await vietMapService.SearchAddressAsync(text, cancellationToken);

        return Ok(new ApiResponse<LocationSearchResponse>
        {
            Success = true,
            Message = "Tìm vị trí bằng VietMap thành công.",
            Data = result
        });
    }

    [AllowAnonymous]
    [HttpGet("suggest-addresses")]
    public async Task<ActionResult<ApiResponse<List<LocationSuggestionResponse>>>> SuggestAddresses(
        [FromQuery] string text,
        [FromQuery] int limit,
        CancellationToken cancellationToken)
    {
        var result = await vietMapService.SuggestAddressesAsync(
            text,
            limit <= 0 ? 5 : limit,
            cancellationToken);

        return Ok(new ApiResponse<List<LocationSuggestionResponse>>
        {
            Success = true,
            Message = "Gợi ý vị trí bằng VietMap thành công.",
            Data = result
        });
    }
}
