using System.Collections.Generic;

namespace SmartRentalPlatform.Contracts.RoomingHouseReviews.Responses;

public class RoomingHouseReviewListResponse
{
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public Dictionary<int, int> RatingDistribution { get; set; } = new();
    public List<RoomingHouseReviewResponse> Reviews { get; set; } = new();
}
