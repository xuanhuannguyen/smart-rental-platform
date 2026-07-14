using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Enums.Properties;

namespace SmartRentalPlatform.Domain.Entities.Properties;

public class RoomingHouseRule
{
    public Guid Id { get; set; }

    public Guid RoomingHouseId { get; set; }

    public RoomingHouseRuleSourceType SourceType { get; set; }

    public Guid? MediaAssetId { get; set; }

    public string? GeneralRules { get; set; }

    public string? QuietHours { get; set; }

    public string? SecurityPolicy { get; set; }

    public string? CleaningPolicy { get; set; }

    public string? GuestPolicy { get; set; }

    public string? ParkingPolicy { get; set; }

    public string? UtilityPolicy { get; set; }

    public string? DamageCompensationPolicy { get; set; }

    public string? AdditionalNotes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public RoomingHouse RoomingHouse { get; set; } = null!;

    public MediaAsset? MediaAsset { get; set; }
}
