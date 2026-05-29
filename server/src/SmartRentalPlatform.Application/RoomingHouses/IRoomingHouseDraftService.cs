using SmartRentalPlatform.Contracts.RoomingHouses;

namespace SmartRentalPlatform.Application.RoomingHouses;

public interface IRoomingHouseDraftService
{
    Task<RoomingHouseDetailResponse> CreateDraftAsync(
        Guid landlordUserId,
        CreateRoomingHouseDraftRequest request,
        CancellationToken cancellationToken = default);

    Task<RoomingHouseDetailResponse?> UpdateAsync(
        Guid roomingHouseId,
        UpdateRoomingHouseRequest request,
        CancellationToken cancellationToken = default);
}
