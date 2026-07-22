using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.ReviewReports;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RoomingHouseReviews.Requests;
using SmartRentalPlatform.Contracts.RoomingHouseReviews.Responses;

namespace SmartRentalPlatform.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/reviews")]
[Authorize(Roles = "Admin")]
public sealed class AdminReviewModerationController : ControllerBase
{
    private readonly IReviewModerationAdminService reviewModerationService;
    private readonly ICurrentUserService currentUserService;

    public AdminReviewModerationController(
        IReviewModerationAdminService reviewModerationService,
        ICurrentUserService currentUserService)
    {
        this.reviewModerationService = reviewModerationService;
        this.currentUserService = currentUserService;
    }

    [HttpGet("moderation")]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminReviewModerationItemResponse>>>> GetReviews(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = "PendingAdminReview",
        CancellationToken cancellationToken = default)
    {
        var result = await reviewModerationService.GetReviewsAsync(page, pageSize, status, cancellationToken);
        return Ok(new ApiResponse<PagedResult<AdminReviewModerationItemResponse>>
        {
            Success = true,
            Message = "Tải danh sách đánh giá chờ duyệt thành công.",
            Data = result
        });
    }

    [HttpPost("{reviewId:guid}/moderation")]
    public async Task<IActionResult> Moderate(
        [FromRoute] Guid reviewId,
        [FromBody] ModerateRoomingHouseReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = currentUserService.GetRequiredUserIdForAction();
        await reviewModerationService.ModerateAsync(
            reviewId,
            adminUserId,
            request.Action,
            request.AdminNote,
            cancellationToken);

        return NoContent();
    }
}
