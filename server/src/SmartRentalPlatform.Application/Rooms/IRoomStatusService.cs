using SmartRentalPlatform.Contracts.Rooms;

namespace SmartRentalPlatform.Application.Rooms;

public interface IRoomStatusService
{
    Task<RoomResponse?> UpdateStatusAsync(
        Guid landlordUserId,
        Guid roomId,
        UpdateRoomStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<RoomResponse?> SubmitAsync(
        Guid landlordUserId,
        Guid roomId,
        CancellationToken cancellationToken = default);
}
