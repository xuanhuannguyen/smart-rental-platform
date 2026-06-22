using SmartRentalPlatform.Contracts.Amenities;

namespace SmartRentalPlatform.Contracts.RoomingHouses.Responses;

/// <summary>
/// Lightweight DTO for home page listing cards.
/// Projected directly via EF Core Select() — no full entity loading.
/// </summary>
public class RoomingHouseListingResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AddressDisplay { get; set; } = string.Empty;

    public string? CoverImageUrl { get; set; }

    public int AvailableRooms { get; set; }

    public decimal? MinMonthlyRent { get; set; }

    public decimal? MaxMonthlyRent { get; set; }

    public decimal? MinAreaM2 { get; set; }

    public decimal? MaxAreaM2 { get; set; }

    public List<AmenityResponse> Amenities { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; }
}
