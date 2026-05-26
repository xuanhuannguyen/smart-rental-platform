using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.LegalDocuments;
using SmartRentalPlatform.Contracts.PropertyImages;
using SmartRentalPlatform.Contracts.RoomingHouses;

namespace SmartRentalPlatform.Application.RoomingHouses
{
    public interface IRoomingHouseService
    {
        Task<RoomingHouseDetailResponse> CreateDraftAsync(Guid landlordUserId, CreateRoomingHouseDraftRequest request, CancellationToken cancellationToken = default);
        Task<RoomingHouseOnboardingResponse> GetOnboardingAsync(Guid landlordUserId, CancellationToken cancellationToken = default);
        Task<List<RoomingHouseResponse>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<List<RoomingHouseResponse>> GetByLandlordAsync(Guid landlordUserId, CancellationToken cancellationToken = default);
        Task<RoomingHouseDetailResponse?> GetByIdAsync(Guid roomingHouseId, CancellationToken cancellationToken = default);
        Task<RoomingHouseDetailResponse?> UpdateAsync(Guid roomingHouseId, UpdateRoomingHouseRequest request, CancellationToken cancellationToken = default);
        Task<RoomingHouseDetailResponse?> SubmitAsync(Guid roomingHouseId, Guid landlordUserId, CancellationToken cancellationToken = default);
        Task<RoomingHouseDetailResponse?> ApproveAsync(Guid roomingHouseId, Guid adminUserId, CancellationToken cancellationToken = default);
        Task<RoomingHouseDetailResponse?> RejectAsync(Guid roomingHouseId, Guid adminUserId, RejectRoomingHouseRequest request, CancellationToken cancellationToken = default);
        Task<RoomingHouseDetailResponse?> UpdateAmenitiesAsync(Guid roomingHouseId, UpdateAmenitiesRequest request, CancellationToken cancellationToken = default);
        Task<RoomingHouseDetailResponse?> UpdateImagesAsync(Guid roomingHouseId, UpdatePropertyImagesRequest request, CancellationToken cancellationToken = default);
        Task<RoomingHouseDetailResponse?> UpdateLegalDocumentAsync(Guid roomingHouseId, UpdateRoomingHouseLegalDocumentRequest request, CancellationToken cancellationToken = default);
    }
}
