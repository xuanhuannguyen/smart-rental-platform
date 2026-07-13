namespace SmartRentalPlatform.Contracts.RoomingHouseReviews.Requests;

public sealed class ModerateRoomingHouseReviewRequest
{
    public string Action { get; set; } = string.Empty;

    public string? AdminNote { get; set; }
}
