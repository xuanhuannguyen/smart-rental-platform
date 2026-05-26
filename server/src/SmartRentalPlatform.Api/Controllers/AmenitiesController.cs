using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Amenities;
using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Route("api/amenities")]
public class AmenitiesController : ControllerBase
{
    private readonly IAmenityService amenityService;

    public AmenitiesController(IAmenityService amenityService)
    {
        this.amenityService = amenityService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<AmenityResponse>>>> GetAmenities(
        [FromQuery] string? scope,
        CancellationToken cancellationToken)
    {
        if (!TryParseScope(scope, out var amenityScope))
        {
            return BadRequest(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "Phạm vi tiện ích phải là House, Room hoặc Both.",
                Details = new { field = nameof(scope), value = scope }
            });
        }

        var result = await amenityService.GetActiveAmenitiesAsync(amenityScope, cancellationToken);

        return Ok(new ApiResponse<List<AmenityResponse>>
        {
            Success = true,
            Message = "Tải danh sách tiện ích thành công.",
            Data = result
        });
    }

    private static bool TryParseScope(string? scope, out AmenityScope? amenityScope)
    {
        amenityScope = null;

        if (string.IsNullOrWhiteSpace(scope))
        {
            return true;
        }

        if (!Enum.TryParse<AmenityScope>(scope, ignoreCase: true, out var parsedScope))
        {
            return false;
        }

        amenityScope = parsedScope;
        return true;
    }
}
