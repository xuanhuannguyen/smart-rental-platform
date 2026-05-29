using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.PropertyImages;
using SmartRentalPlatform.Contracts.Rooms;

namespace SmartRentalPlatform.Application.Rooms;

public interface IRoomMediaService
{
    Task<RoomResponse?> UpdateAmenitiesAsync(
        Guid landlordUserId,
        Guid roomId,
        UpdateAmenitiesRequest request,
        CancellationToken cancellationToken = default);

    Task<RoomResponse?> UpdateImagesAsync(
        Guid landlordUserId,
        Guid roomId,
        UpdatePropertyImagesRequest request,
        CancellationToken cancellationToken = default);
}
