using SmartRentalPlatform.Contracts.RoomPriceTiers;
using SmartRentalPlatform.Contracts.Rooms;

namespace SmartRentalPlatform.Application.Rooms;

public interface IRoomPriceTierService
{
    Task<RoomResponse?> UpdatePriceTiersAsync(
        Guid landlordUserId,
        Guid roomId,
        UpdateRoomPriceTiersRequest request,
        CancellationToken cancellationToken = default);
}
