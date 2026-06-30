namespace SmartRentalPlatform.Contracts.RoomingHouses.Responses;

public class NearbyPlaceResponse
{
    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? DisplayAddress { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public decimal? DistanceKm { get; set; }

    public string? Category { get; set; }
}
