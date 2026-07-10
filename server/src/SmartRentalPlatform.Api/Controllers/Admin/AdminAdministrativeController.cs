using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Administrative;
using SmartRentalPlatform.Contracts.Administrative.Requests;
using SmartRentalPlatform.Contracts.Administrative.Responses;
using SmartRentalPlatform.Contracts.Common;
using System.Threading;
using System.Threading.Tasks;

namespace SmartRentalPlatform.Api.Controllers.Admin;

[Route("api/admin/administrative")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminAdministrativeController : ControllerBase
{
    private readonly IAdministrativeService _administrativeService;

    public AdminAdministrativeController(IAdministrativeService administrativeService)
    {
        _administrativeService = administrativeService;
    }

    [HttpGet("provinces")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AdminProvinceResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProvinces(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _administrativeService.GetProvincesAsync(page, pageSize, keyword, cancellationToken);
        return Ok(new ApiResponse<PagedResult<AdminProvinceResponse>>
        {
            Data = result,
            Success = true,
            Message = "Thành công"
        });
    }

    [HttpGet("provinces/{code}")]
    [ProducesResponseType(typeof(ApiResponse<AdminProvinceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProvince(
        [FromRoute] string code,
        CancellationToken cancellationToken)
    {
        var result = await _administrativeService.GetProvinceAsync(code, cancellationToken);
        return Ok(new ApiResponse<AdminProvinceResponse>
        {
            Data = result,
            Success = true,
            Message = "Thành công"
        });
    }

    [HttpPost("provinces")]
    [ProducesResponseType(typeof(ApiResponse<AdminProvinceResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateProvince(
        [FromBody] CreateProvinceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _administrativeService.CreateProvinceAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetProvince), new { code = result.Code }, new ApiResponse<AdminProvinceResponse>
        {
            Data = result,
            Success = true,
            Message = "Tạo tỉnh/thành phố thành công"
        });
    }

    [HttpPut("provinces/{code}")]
    [ProducesResponseType(typeof(ApiResponse<AdminProvinceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProvince(
        [FromRoute] string code,
        [FromBody] UpdateProvinceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _administrativeService.UpdateProvinceAsync(code, request, cancellationToken);
        return Ok(new ApiResponse<AdminProvinceResponse>
        {
            Data = result,
            Success = true,
            Message = "Cập nhật tỉnh/thành phố thành công"
        });
    }

    [HttpPatch("provinces/{code}/toggle-active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ToggleProvinceActive(
        [FromRoute] string code,
        CancellationToken cancellationToken)
    {
        await _administrativeService.ToggleProvinceActiveAsync(code, cancellationToken);
        return NoContent();
    }

    [HttpGet("wards")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AdminWardResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWards(
        [FromQuery] string provinceCode,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _administrativeService.GetWardsAsync(provinceCode, page, pageSize, keyword, cancellationToken);
        return Ok(new ApiResponse<PagedResult<AdminWardResponse>>
        {
            Data = result,
            Success = true,
            Message = "Thành công"
        });
    }

    [HttpGet("wards/{code}")]
    [ProducesResponseType(typeof(ApiResponse<AdminWardResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWard(
        [FromRoute] string code,
        CancellationToken cancellationToken)
    {
        var result = await _administrativeService.GetWardAsync(code, cancellationToken);
        return Ok(new ApiResponse<AdminWardResponse>
        {
            Data = result,
            Success = true,
            Message = "Thành công"
        });
    }

    [HttpPost("wards")]
    [ProducesResponseType(typeof(ApiResponse<AdminWardResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateWard(
        [FromBody] CreateWardRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _administrativeService.CreateWardAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetWard), new { code = result.Code }, new ApiResponse<AdminWardResponse>
        {
            Data = result,
            Success = true,
            Message = "Tạo phường/xã thành công"
        });
    }

    [HttpPut("wards/{code}")]
    [ProducesResponseType(typeof(ApiResponse<AdminWardResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateWard(
        [FromRoute] string code,
        [FromBody] UpdateWardRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _administrativeService.UpdateWardAsync(code, request, cancellationToken);
        return Ok(new ApiResponse<AdminWardResponse>
        {
            Data = result,
            Success = true,
            Message = "Cập nhật phường/xã thành công"
        });
    }

    [HttpPatch("wards/{code}/toggle-active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ToggleWardActive(
        [FromRoute] string code,
        CancellationToken cancellationToken)
    {
        await _administrativeService.ToggleWardActiveAsync(code, cancellationToken);
        return NoContent();
    }
}
