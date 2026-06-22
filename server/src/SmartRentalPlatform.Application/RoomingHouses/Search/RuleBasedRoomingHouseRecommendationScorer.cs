namespace SmartRentalPlatform.Application.RoomingHouses.Search;

public sealed class RuleBasedRoomingHouseRecommendationScorer : IRoomingHouseRecommendationScorer
{
    public int CalculateBehaviorScore(RoomingHouseSearchCandidate candidate)
    {
        var data = candidate.CandidateData;
        var criteria = candidate.Criteria;
        var score = 0;

        if (criteria.RecentRoomingHouseIds.Contains(data.Id))
        {
            score -= 20;
        }

        score += criteria.PreferredAmenityIds.Count(id =>
            data.HouseAmenities.Any(amenity => amenity.Id == id)) * 10;

        score += criteria.PreferredRoomAmenityIds.Count(id =>
            data.AvailableRooms.Any(room =>
                room.RoomAmenities.Any(amenity => amenity.Id == id))) * 10;

        return score;
    }
}
