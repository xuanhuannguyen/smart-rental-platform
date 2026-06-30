using SmartRentalPlatform.Contracts.Rooms;

namespace SmartRentalPlatform.Application.Rooms;

public interface IRoomQueryService
{
    Task<List<RoomResponse>> GetByRoomingHouseAsync(
        Guid landlordUserId,
        Guid roomingHouseId,
        CancellationToken cancellationToken = default);

    Task<RoomResponse?> GetByIdAsync(
        Guid landlordUserId,
        Guid roomId,
        CancellationToken cancellationToken = default);

    Task<RoomResponse?> GetPublicRoomByIdAsync(
        Guid roomId,
        CancellationToken cancellationToken = default);

    Task<List<RoomResponse>> GetPublicAvailableRoomsAsync(
        Guid roomingHouseId,
        CancellationToken cancellationToken = default);
}
