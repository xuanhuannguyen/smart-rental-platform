using SmartRentalPlatform.Contracts.RoomingHouses;

namespace SmartRentalPlatform.Application.RoomingHouses;

public interface IRoomingHouseSubmissionService
{
    Task<RoomingHouseDetailResponse?> SubmitAsync(
        Guid roomingHouseId,
        Guid landlordUserId,
        CancellationToken cancellationToken = default);
}
