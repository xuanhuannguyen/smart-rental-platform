namespace SmartRentalPlatform.Contracts.RoomingHouses.Requests;

public class GuestRoomingHouseRecommendationRequest
{
    public List<string> RecentQueries { get; set; } = new();

    public List<Guid> RecentRoomingHouseIds { get; set; } = new();

    public List<Guid> ClickedRoomingHouseIds { get; set; } = new();

    public List<int> PreferredAmenityIds { get; set; } = new();

    public List<int> PreferredRoomAmenityIds { get; set; } = new();

    public string? ProvinceCode { get; set; }

    public string? WardCode { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public decimal? MinAreaM2 { get; set; }

    public decimal? MaxAreaM2 { get; set; }

    public int PageSize { get; set; } = 8;
}
