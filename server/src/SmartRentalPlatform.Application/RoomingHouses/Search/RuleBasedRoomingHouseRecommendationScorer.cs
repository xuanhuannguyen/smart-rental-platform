namespace SmartRentalPlatform.Application.RoomingHouses.Search;

public sealed class RuleBasedRoomingHouseRecommendationScorer : IRoomingHouseRecommendationScorer
{
    public int CalculateBehaviorScore(RoomingHouseSearchCandidate candidate)
    {
        var data = candidate.CandidateData;
        var criteria = candidate.Criteria;
        var score = 0;

        // Penalty for recently viewed houses (decay based on relative position)
        // RecentRoomingHouseIds are ordered newest-first, so earlier index = more recent = larger penalty
        for (var i = 0; i < criteria.RecentRoomingHouseIds.Count; i++)
        {
            if (criteria.RecentRoomingHouseIds[i] == data.Id)
            {
                // Linear decay: first position gets -20, second -15, third -10, rest -5
                score -= Math.Max(5, 20 - (i * 5));
                break;
            }
        }

        // Match preferred amenities
        score += criteria.PreferredAmenityIds.Count(id =>
            data.HouseAmenities.Any(amenity => amenity.Id == id)) * 12;

        score += criteria.PreferredRoomAmenityIds.Count(id =>
            data.AvailableRooms.Any(room =>
                room.RoomAmenities.Any(amenity => amenity.Id == id))) * 10;

        // Bonus for houses with cover images
        if (data.ImageCount > 0)
        {
            score += data.ImageCount >= 3 ? 12 : 6;
        }

        // Bonus for high occupancy availability ratio
        var totalRoomCount = data.AvailableRooms.Count;
        // AvailableRooms count is the matching filtered rooms count — we use the total
        // from the candidate data to compute ratio. Since we don't have TotalRooms
        // in this DTO, we use a simpler heuristic based on room count
        if (data.AvailableRooms.Count > 3)
        {
            score += 8;
        }

        // KYC trust bonus
        if (data.HasVerifiedKyc)
        {
            score += 15;
        }

        // Freshness bonus (houses created/updated within 7 days)
        var ageDays = (DateTimeOffset.UtcNow - data.UpdatedAt).TotalDays;
        if (ageDays <= 7)
        {
            score += 10;
        }
        else if (ageDays <= 30)
        {
            score += 5;
        }

        return score;
    }
}
