namespace SmartRentalPlatform.Contracts.RoomingHouses.Requests;

public class RoomingHouseBasicInfoRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string AddressLine { get; set; } = string.Empty;

    public string ProvinceCode { get; set; } = string.Empty;

    public string WardCode { get; set; } = string.Empty;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }
}
