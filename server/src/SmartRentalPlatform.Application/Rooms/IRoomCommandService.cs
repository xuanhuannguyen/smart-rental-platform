using SmartRentalPlatform.Contracts.Rooms;

namespace SmartRentalPlatform.Application.Rooms;

public interface IRoomCommandService
{
    Task<RoomResponse> CreateAsync(
        Guid landlordUserId,
        Guid roomingHouseId,
        CreateRoomRequest request,
        CancellationToken cancellationToken = default);

    Task<RoomResponse?> UpdateAsync(
        Guid landlordUserId,
        Guid roomId,
        UpdateRoomRequest request,
        CancellationToken cancellationToken = default);
}
