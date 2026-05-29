using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.LegalDocuments;
using SmartRentalPlatform.Contracts.PropertyImages;
using SmartRentalPlatform.Contracts.RoomingHouses;

namespace SmartRentalPlatform.Application.RoomingHouses;

public interface IRoomingHouseMediaService
{
    Task<RoomingHouseDetailResponse?> UpdateAmenitiesAsync(
        Guid roomingHouseId,
        UpdateAmenitiesRequest request,
        CancellationToken cancellationToken = default);

    Task<RoomingHouseDetailResponse?> UpdateImagesAsync(
        Guid roomingHouseId,
        UpdatePropertyImagesRequest request,
        CancellationToken cancellationToken = default);

    Task<RoomingHouseDetailResponse?> UpdateLegalDocumentAsync(
        Guid roomingHouseId,
        UpdateRoomingHouseLegalDocumentRequest request,
        CancellationToken cancellationToken = default);
}
