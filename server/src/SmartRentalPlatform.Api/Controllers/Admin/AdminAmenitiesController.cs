using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Amenities;
using SmartRentalPlatform.Contracts.Amenities.Requests;
using SmartRentalPlatform.Contracts.Amenities.Responses;
using SmartRentalPlatform.Contracts.Common;
using System.Threading;
using System.Threading.Tasks;

namespace SmartRentalPlatform.Api.Controllers.Admin;

[Route("api/admin/amenities")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminAmenitiesController : ControllerBase
{
    private readonly IAmenityService _amenityService;

    public AdminAmenitiesController(IAmenityService amenityService)
    {
        _amenityService = amenityService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AdminAmenityResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAmenities(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _amenityService.GetAmenitiesAsync(page, pageSize, keyword, cancellationToken);
        return Ok(new ApiResponse<PagedResult<AdminAmenityResponse>>
        {
            Data = result,
            Success = true,
            Message = "Thành công"
        });
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<AdminAmenityResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAmenity(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var result = await _amenityService.GetAmenityAsync(id, cancellationToken);
        return Ok(new ApiResponse<AdminAmenityResponse>
        {
            Data = result,
            Success = true,
            Message = "Thành công"
        });
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AdminAmenityResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAmenity(
        [FromBody] CreateAmenityRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _amenityService.CreateAmenityAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetAmenity), new { id = result.Id }, new ApiResponse<AdminAmenityResponse>
        {
            Data = result,
            Success = true,
            Message = "Tạo tiện ích thành công"
        });
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<AdminAmenityResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAmenity(
        [FromRoute] int id,
        [FromBody] UpdateAmenityRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _amenityService.UpdateAmenityAsync(id, request, cancellationToken);
        return Ok(new ApiResponse<AdminAmenityResponse>
        {
            Data = result,
            Success = true,
            Message = "Cập nhật tiện ích thành công"
        });
    }

    [HttpPatch("{id}/toggle-active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ToggleAmenityActive(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        await _amenityService.ToggleAmenityActiveAsync(id, cancellationToken);
        return NoContent();
    }
}
