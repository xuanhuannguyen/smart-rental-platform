using SmartRentalPlatform.Domain.Entities.Properties;

namespace SmartRentalPlatform.Application.RoomingHouses.ReviewModeration;

public interface IReviewAiModerationService
{
    Task ModerateReviewAsync(
        Guid reviewId,
        CancellationToken cancellationToken = default);

    Task<int> ModeratePendingReviewsAsync(
        int batchSize = 20,
        CancellationToken cancellationToken = default);
}
