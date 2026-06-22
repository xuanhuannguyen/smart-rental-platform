namespace SmartRentalPlatform.Application.RoomingHouses.Search;

public interface IRoomingHouseRecommendationScorer
{
    int CalculateBehaviorScore(RoomingHouseSearchCandidate candidate);
}
