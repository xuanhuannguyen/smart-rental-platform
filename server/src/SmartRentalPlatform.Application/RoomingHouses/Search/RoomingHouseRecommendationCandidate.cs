namespace SmartRentalPlatform.Application.RoomingHouses.Search;

public sealed class RoomingHouseRecommendationCandidate
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AddressDisplay { get; set; } = string.Empty;

    public decimal? DistanceKm { get; set; }

    public decimal? MinMonthlyRent { get; set; }

    public decimal? MaxMonthlyRent { get; set; }

    public decimal? MinAreaM2 { get; set; }

    public decimal? MaxAreaM2 { get; set; }

    public int AvailableRooms { get; set; }

    /// <summary>Total rooms in the house — used to compute occupancy ratio.</summary>
    public int TotalRooms { get; set; }

    /// <summary>Whether the house has a cover image (visual quality signal).</summary>
    public bool HasCoverImage { get; set; }

    /// <summary>House creation date — for freshness scoring.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    public List<string> Amenities { get; set; } = new();
}
