using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.RoomingHouses;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RoomingHouseReviews.Requests;
using SmartRentalPlatform.Contracts.RoomingHouseReviews.Responses;

namespace SmartRentalPlatform.Api.Controllers.Properties;

[Route("api/rooming-houses")]
[ApiController]
public class RoomingHouseReviewsController : ControllerBase
{
    private readonly IRoomingHouseReviewService _reviewService;
    private readonly ICurrentUserService _currentUserService;

    public RoomingHouseReviewsController(
        IRoomingHouseReviewService reviewService,
        ICurrentUserService currentUserService)
    {
        _reviewService = reviewService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Lấy danh sách reviews của một khu trọ (Public)
    /// </summary>
    [HttpGet("{roomingHouseId}/reviews")]
    [ProducesResponseType(typeof(ApiResponse<RoomingHouseReviewListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReviews(
        [FromRoute] Guid roomingHouseId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _reviewService.GetReviewsAsync(roomingHouseId, page, pageSize, cancellationToken);
        return Ok(new ApiResponse<RoomingHouseReviewListResponse> { Success = true, Data = result });
    }

    /// <summary>
    /// Kiểm tra điều kiện có thể review cho hợp đồng (Tenant)
    /// </summary>
    [HttpGet("contracts/{contractId}/review-eligibility")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ReviewEligibilityResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckEligibility(
        [FromRoute] Guid contractId,
        CancellationToken cancellationToken)
    {
        var tenantUserId = _currentUserService.GetRequiredUserIdForAction();
        var result = await _reviewService.CheckEligibilityAsync(contractId, tenantUserId, cancellationToken);
        return Ok(new ApiResponse<ReviewEligibilityResponse> { Success = true, Data = result });
    }

    /// <summary>
    /// Kiểm tra điều kiện review của tenant đối với cả khu trọ (Public detail page)
    /// </summary>
    [HttpGet("{roomingHouseId}/review-eligibility-summary")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<RoomingHouseReviewEligibilitySummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReviewEligibilitySummary(
        [FromRoute] Guid roomingHouseId,
        CancellationToken cancellationToken)
    {
        var tenantUserId = _currentUserService.GetRequiredUserIdForAction();
        var result = await _reviewService.CheckRoomingHouseEligibilityAsync(roomingHouseId, tenantUserId, cancellationToken);
        return Ok(new ApiResponse<RoomingHouseReviewEligibilitySummaryResponse> { Success = true, Data = result });
    }

    /// <summary>
    /// Tạo đánh giá cho khu trọ từ một hợp đồng (Tenant)
    /// </summary>
    [HttpPost("contracts/{contractId}/reviews")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<RoomingHouseReviewResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateReview(
        [FromRoute] Guid contractId,
        [FromForm] CreateRoomingHouseReviewRequest request,
        CancellationToken cancellationToken)
    {
        var tenantUserId = _currentUserService.GetRequiredUserIdForAction();
        var result = await _reviewService.CreateReviewAsync(contractId, tenantUserId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new ApiResponse<RoomingHouseReviewResponse> { Success = true, Data = result });
    }

    /// <summary>
    /// Chỉnh sửa đánh giá (Tenant) - Chỉ trong vòng 7 ngày
    /// </summary>
    [HttpPut("reviews/{reviewId}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<RoomingHouseReviewResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateReview(
        [FromRoute] Guid reviewId,
        [FromForm] UpdateRoomingHouseReviewRequest request,
        CancellationToken cancellationToken)
    {
        var tenantUserId = _currentUserService.GetRequiredUserIdForAction();
        var result = await _reviewService.UpdateReviewAsync(reviewId, tenantUserId, request, cancellationToken);
        return Ok(new ApiResponse<RoomingHouseReviewResponse> { Success = true, Data = result });
    }

    /// <summary>
    /// Xóa đánh giá (Tenant) - Chỉ trong vòng 7 ngày
    /// </summary>
    [HttpDelete("reviews/{reviewId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteReview(
        [FromRoute] Guid reviewId,
        CancellationToken cancellationToken)
    {
        var tenantUserId = _currentUserService.GetRequiredUserIdForAction();
        await _reviewService.DeleteReviewAsync(reviewId, tenantUserId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Phản hồi đánh giá (Landlord)
    /// </summary>
    [HttpPost("reviews/{reviewId}/reply")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReplyReview(
        [FromRoute] Guid reviewId,
        [FromBody] ReplyRoomingHouseReviewRequest request,
        CancellationToken cancellationToken)
    {
        var landlordUserId = _currentUserService.GetRequiredUserIdForAction();
        await _reviewService.ReplyReviewAsync(reviewId, landlordUserId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Xóa phản hồi (Landlord)
    /// </summary>
    [HttpDelete("reviews/{reviewId}/reply")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteReply(
        [FromRoute] Guid reviewId,
        CancellationToken cancellationToken)
    {
        var landlordUserId = _currentUserService.GetRequiredUserIdForAction();
        await _reviewService.DeleteReplyAsync(reviewId, landlordUserId, cancellationToken);
        return NoContent();
    }
}
