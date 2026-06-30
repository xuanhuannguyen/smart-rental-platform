using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RoomingHouses;
using SmartRentalPlatform.Contracts.RoomingHouses.Requests;
using SmartRentalPlatform.Contracts.RoomingHouses.Responses;

namespace SmartRentalPlatform.Application.RoomingHouses;

public interface IRoomingHouseQueryService
{
    Task<RoomingHouseOnboardingResponse> GetOnboardingAsync(
        Guid landlordUserId,
        CancellationToken cancellationToken = default);

    Task<List<RoomingHouseDetailResponse>> GetPublicAvailableAsync(
        CancellationToken cancellationToken = default);

    Task<List<RoomingHouseListingResponse>> GetPublicListingAsync(
        CancellationToken cancellationToken = default);

    Task<PagedResult<RoomingHouseSearchItemResponse>> SearchPublicAsync(
        RoomingHouseSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<RoomingHouseRecommendationResponse> GetGuestRecommendationsAsync(
        GuestRoomingHouseRecommendationRequest request,
        CancellationToken cancellationToken = default);

    Task<RoomingHouseDetailResponse?> GetPublicByIdAsync(
        Guid roomingHouseId,
        CancellationToken cancellationToken = default);

    Task<List<RoomingHouseResponse>> GetByLandlordAsync(
        Guid landlordUserId,
        CancellationToken cancellationToken = default);

    Task<RoomingHouseDetailResponse?> GetByIdAsync(
        Guid roomingHouseId,
        CancellationToken cancellationToken = default);
}
