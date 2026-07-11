using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Billing;
using SmartRentalPlatform.Contracts.Billing.Requests;
using SmartRentalPlatform.Contracts.Billing.Responses;
using SmartRentalPlatform.Contracts.Common;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SmartRentalPlatform.Api.Controllers.Admin;

[Route("api/admin/billing-service-types")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminBillingServiceTypesController : ControllerBase
{
    private readonly IBillingService _billingService;

    public AdminBillingServiceTypesController(IBillingService billingService)
    {
        _billingService = billingService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AdminBillingServiceTypeResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBillingServiceTypes(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _billingService.GetBillingServiceTypesAdminAsync(page, pageSize, keyword, cancellationToken);
        return Ok(new ApiResponse<PagedResult<AdminBillingServiceTypeResponse>>
        {
            Data = result,
            Success = true,
            Message = "Thành công"
        });
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<AdminBillingServiceTypeResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBillingServiceType(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _billingService.GetBillingServiceTypeAdminAsync(id, cancellationToken);
        return Ok(new ApiResponse<AdminBillingServiceTypeResponse>
        {
            Data = result,
            Success = true,
            Message = "Thành công"
        });
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AdminBillingServiceTypeResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateBillingServiceType(
        [FromBody] CreateBillingServiceTypeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _billingService.CreateBillingServiceTypeAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetBillingServiceType), new { id = result.Id }, new ApiResponse<AdminBillingServiceTypeResponse>
        {
            Data = result,
            Success = true,
            Message = "Tạo loại dịch vụ thành công"
        });
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<AdminBillingServiceTypeResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateBillingServiceType(
        [FromRoute] Guid id,
        [FromBody] UpdateBillingServiceTypeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _billingService.UpdateBillingServiceTypeAsync(id, request, cancellationToken);
        return Ok(new ApiResponse<AdminBillingServiceTypeResponse>
        {
            Data = result,
            Success = true,
            Message = "Cập nhật loại dịch vụ thành công"
        });
    }

    [HttpPatch("{id}/toggle-active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ToggleBillingServiceTypeActive(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        await _billingService.ToggleBillingServiceTypeActiveAsync(id, cancellationToken);
        return NoContent();
    }
}
