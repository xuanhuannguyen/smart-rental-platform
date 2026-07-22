using SmartRentalPlatform.Contracts.Amenities;

namespace SmartRentalPlatform.Contracts.RoomingHouses.Responses;

public class RoomingHouseSearchItemResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AddressDisplay { get; set; } = string.Empty;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public decimal? DistanceKm { get; set; }

    public string? CoverImageUrl { get; set; }

    public int AvailableRooms { get; set; }

    public int TotalRooms { get; set; }

    public decimal? MinMonthlyRent { get; set; }

    public decimal? MaxMonthlyRent { get; set; }

    public decimal? MinAreaM2 { get; set; }

    public decimal? MaxAreaM2 { get; set; }

    public List<AmenityResponse> Amenities { get; set; } = new();

    public double AverageRating { get; set; }

    public int TotalReviews { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
