namespace SmartRentalPlatform.Contracts.Locations;

public class LocationSearchResponse
{
    public string? RefId { get; set; }

    public string DisplayAddress { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? Address { get; set; }

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }
}
