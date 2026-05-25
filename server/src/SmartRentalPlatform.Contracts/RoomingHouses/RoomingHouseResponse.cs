namespace SmartRentalPlatform.Contracts.RoomingHouses;

public class RoomingHouseResponse
{
    public Guid Id { get; set; }

    public Guid LandlordUserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AddressDisplay { get; set; } = string.Empty;

    public string ApprovalStatus { get; set; } = string.Empty;

    public string VisibilityStatus { get; set; } = string.Empty;

    public string? RejectedReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
