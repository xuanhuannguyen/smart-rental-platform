using SmartRentalPlatform.Contracts.RoomingHouses.Requests;

namespace SmartRentalPlatform.Application.RoomingHouses.Search;

public interface IRoomingHouseRecommendationReranker
{
    Task<RoomingHouseRecommendationRerankResult?> RerankAsync(
        GuestRoomingHouseRecommendationRequest request,
        IReadOnlyList<RoomingHouseRecommendationCandidate> candidates,
        CancellationToken cancellationToken = default);
}
