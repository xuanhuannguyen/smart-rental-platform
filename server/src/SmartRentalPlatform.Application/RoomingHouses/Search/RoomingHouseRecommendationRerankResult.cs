namespace SmartRentalPlatform.Application.RoomingHouses.Search;

public sealed class RoomingHouseRecommendationRerankResult
{
    public List<Guid> RankedIds { get; set; } = new();

    public Dictionary<Guid, string> Reasons { get; set; } = new();
}
