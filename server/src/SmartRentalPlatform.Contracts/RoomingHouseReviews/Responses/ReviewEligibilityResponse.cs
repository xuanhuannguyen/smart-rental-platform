namespace SmartRentalPlatform.Contracts.RoomingHouseReviews.Responses;

public class ReviewEligibilityResponse
{
    public bool IsEligible { get; set; }
    public string? Reason { get; set; }
}
