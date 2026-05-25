using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.PropertyImages;
using SmartRentalPlatform.Contracts.RoomPriceTiers;
using SmartRentalPlatform.Contracts.Rooms;

namespace SmartRentalPlatform.Application.Rooms
{
    public interface IRoomService
    {
        Task<RoomResponse> CreateAsync(Guid landlordUserId, Guid roomingHouseId, CreateRoomRequest request, CancellationToken cancellationToken = default);

        Task<List<RoomResponse>> GetByRoomingHouseAsync(Guid landlordUserId, Guid roomingHouseId, CancellationToken cancellationToken = default);

        Task<RoomResponse?> GetByIdAsync(Guid landlordUserId, Guid roomId, CancellationToken cancellationToken = default);

        Task<RoomResponse?> UpdateAsync(Guid landlordUserId, Guid roomId, UpdateRoomRequest request, CancellationToken cancellationToken = default);

        Task<RoomResponse?> UpdateAmenitiesAsync(Guid landlordUserId, Guid roomId, UpdateAmenitiesRequest request, CancellationToken cancellationToken = default);

        Task<RoomResponse?> UpdateImagesAsync(Guid landlordUserId, Guid roomId, UpdatePropertyImagesRequest request, CancellationToken cancellationToken = default);

        Task<RoomResponse?> UpdatePriceTiersAsync(Guid landlordUserId, Guid roomId, UpdateRoomPriceTiersRequest request, CancellationToken cancellationToken = default);

        Task<RoomResponse?> UpdateStatusAsync(Guid landlordUserId, Guid roomId, UpdateRoomStatusRequest request, CancellationToken cancellationToken = default);

        Task<RoomResponse?> SubmitAsync(Guid landlordUserId, Guid roomId, CancellationToken cancellationToken = default);
    }
}
