using System;
using System.Threading;
using System.Threading.Tasks;
using SmartRentalPlatform.Contracts.RoomingHouseReviews.Requests;
using SmartRentalPlatform.Contracts.RoomingHouseReviews.Responses;

namespace SmartRentalPlatform.Application.RoomingHouses;

public interface IRoomingHouseReviewService
{
    Task<RoomingHouseReviewResponse> CreateReviewAsync(
        Guid contractId,
        Guid tenantUserId,
        CreateRoomingHouseReviewRequest request,
        CancellationToken cancellationToken = default);

    Task<ReviewEligibilityResponse> CheckEligibilityAsync(
        Guid contractId,
        Guid tenantUserId,
        CancellationToken cancellationToken = default);

    Task<RoomingHouseReviewEligibilitySummaryResponse> CheckRoomingHouseEligibilityAsync(
        Guid roomingHouseId,
        Guid tenantUserId,
        CancellationToken cancellationToken = default);

    Task<RoomingHouseReviewListResponse> GetReviewsAsync(
        Guid roomingHouseId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<RoomingHouseReviewResponse> UpdateReviewAsync(
        Guid reviewId,
        Guid tenantUserId,
        UpdateRoomingHouseReviewRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteReviewAsync(
        Guid reviewId,
        Guid tenantUserId,
        CancellationToken cancellationToken = default);

    Task ReplyReviewAsync(
        Guid reviewId,
        Guid landlordUserId,
        ReplyRoomingHouseReviewRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteReplyAsync(
        Guid reviewId,
        Guid landlordUserId,
        CancellationToken cancellationToken = default);
}
