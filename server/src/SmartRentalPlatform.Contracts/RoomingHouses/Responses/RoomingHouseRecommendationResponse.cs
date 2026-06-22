namespace SmartRentalPlatform.Contracts.RoomingHouses.Responses;

public class RoomingHouseRecommendationResponse
{
    public List<RoomingHouseSearchItemResponse> Items { get; set; } = new();

    public Dictionary<Guid, string> Reasons { get; set; } = new();

    public bool AiAssisted { get; set; }

    public string? FallbackReason { get; set; }
}
