using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.ReviewReports;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.ReviewReports.Requests;
using SmartRentalPlatform.Contracts.ReviewReports.Responses;

namespace SmartRentalPlatform.Api.Controllers.Admin;

[Route("api/admin/review-reports")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminReviewReportsController : ControllerBase
{
    private readonly IReviewReportService _reportService;
    private readonly ICurrentUserService _currentUserService;

    public AdminReviewReportsController(
        IReviewReportService reportService,
        ICurrentUserService currentUserService)
    {
        _reportService = reportService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Lấy danh sách các báo cáo đánh giá
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ReviewReportResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReports(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _reportService.GetReportsAsync(page, pageSize, status, cancellationToken);
        return Ok(new ApiResponse<PagedResult<ReviewReportResponse>>
        {
            Data = result,
            Success = true,
            Message = "Thành công"
        });
    }

    /// <summary>
    /// Lấy chi tiết báo cáo
    /// </summary>
    [HttpGet("{reportId}")]
    [ProducesResponseType(typeof(ApiResponse<ReviewReportResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReportDetail(
        [FromRoute] Guid reportId,
        CancellationToken cancellationToken)
    {
        var result = await _reportService.GetReportDetailAsync(reportId, cancellationToken);
        return Ok(new ApiResponse<ReviewReportResponse>
        {
            Data = result,
            Success = true,
            Message = "Thành công"
        });
    }

    /// <summary>
    /// Xử lý báo cáo
    /// </summary>
    [HttpPost("{reportId}/resolve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResolveReport(
        [FromRoute] Guid reportId,
        [FromBody] ResolveReviewReportRequest request,
        CancellationToken cancellationToken)
    {
        var adminUserId = _currentUserService.GetRequiredUserIdForAction();
        await _reportService.ResolveReportAsync(reportId, adminUserId, request.HideReview, request.AdminNote, cancellationToken);
        return NoContent();
    }
}
