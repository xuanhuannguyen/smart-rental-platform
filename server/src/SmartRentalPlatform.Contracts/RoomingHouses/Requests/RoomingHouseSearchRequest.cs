namespace SmartRentalPlatform.Contracts.RoomingHouses.Requests;

public class RoomingHouseSearchRequest
{
    public string? Q { get; set; }

    public string? ProvinceCode { get; set; }

    public string? WardCode { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public decimal? MinArea { get; set; }

    public decimal? MaxArea { get; set; }

    public int? MinOccupants { get; set; }

    public List<int> AmenityIds { get; set; } = new();

    public List<int> RoomAmenityIds { get; set; } = new();

    public decimal? CenterLat { get; set; }

    public decimal? CenterLng { get; set; }

    public decimal? RadiusKm { get; set; }

    public string? Sort { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 12;
}
