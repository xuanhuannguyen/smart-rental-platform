namespace SmartRentalPlatform.Contracts.RoomingHouseRules.Responses;

public class RoomingHouseRuleResponse
{
    public Guid Id { get; set; }

    public Guid RoomingHouseId { get; set; }

    public string SourceType { get; set; } = string.Empty;

    public string PdfObjectKey { get; set; } = string.Empty;

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
}
