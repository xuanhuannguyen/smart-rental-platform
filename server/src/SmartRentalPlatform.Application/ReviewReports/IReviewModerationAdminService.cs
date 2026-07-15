using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RoomingHouseReviews.Responses;

namespace SmartRentalPlatform.Application.ReviewReports;

public interface IReviewModerationAdminService
{
    Task<PagedResult<AdminReviewModerationItemResponse>> GetReviewsAsync(
        int page,
        int pageSize,
        string? status = null,
        CancellationToken cancellationToken = default);

    Task ModerateAsync(
        Guid reviewId,
        Guid adminUserId,
        string action,
        string? adminNote,
        CancellationToken cancellationToken = default);
}
