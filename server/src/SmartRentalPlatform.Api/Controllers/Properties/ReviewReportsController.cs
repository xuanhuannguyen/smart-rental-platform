using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.ReviewReports;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Contracts.ReviewReports.Requests;

namespace SmartRentalPlatform.Api.Controllers.Properties;

[Route("api/rooming-houses")]
[ApiController]
public class ReviewReportsController : ControllerBase
{
    private readonly IReviewReportService _reportService;
    private readonly ICurrentUserService _currentUserService;

    public ReviewReportsController(
        IReviewReportService reportService,
        ICurrentUserService currentUserService)
    {
        _reportService = reportService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Báo cáo một đánh giá (Landlord)
    /// </summary>
    [HttpPost("reviews/{reviewId}/report")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CreateReport(
        [FromRoute] Guid reviewId,
        [FromBody] CreateReviewReportRequest request,
        CancellationToken cancellationToken)
    {
        var landlordUserId = _currentUserService.GetRequiredUserIdForAction();
        await _reportService.CreateReportAsync(reviewId, landlordUserId, request, cancellationToken);
        return NoContent();
    }
}
