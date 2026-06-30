namespace SmartRentalPlatform.Application.RoomingHouses.Search;

/// <summary>
/// Flat, lightweight DTO for scoring rooming house search candidates.
/// Loaded via EF Core .Select() projection — no full entity graph materialization.
/// </summary>
public sealed class RoomingHouseSearchCandidateData
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AddressDisplay { get; set; } = string.Empty;

    public string? AddressLine { get; set; }

    public string? Description { get; set; }

    public string? ProvinceCode { get; set; }

    public string? WardCode { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? ProvinceName { get; set; }

    public string? WardName { get; set; }

    public int ImageCount { get; set; }

    public bool HasVerifiedKyc { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public List<CandidateAmenity> HouseAmenities { get; set; } = new();

    public List<CandidateRoom> AvailableRooms { get; set; } = new();
}

public sealed class CandidateRoom
{
    public Guid Id { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public int Floor { get; set; }

    public decimal? AreaM2 { get; set; }

    public int MaxOccupants { get; set; }

    public List<decimal> ActivePrices { get; set; } = new();

    public List<CandidateAmenity> RoomAmenities { get; set; } = new();

    public int ImageCount { get; set; }
}

public sealed class CandidateAmenity
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
