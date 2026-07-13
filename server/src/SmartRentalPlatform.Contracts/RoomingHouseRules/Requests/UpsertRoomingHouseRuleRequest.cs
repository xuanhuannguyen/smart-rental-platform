namespace SmartRentalPlatform.Contracts.RoomingHouseRules.Requests;

public class UpsertRoomingHouseRuleRequest
{
    public string SourceType { get; set; } = string.Empty;

    public Guid? PdfMediaAssetId { get; set; }

    public string? PdfObjectKey { get; set; }

    public string? GeneralRules { get; set; }

    public string? QuietHours { get; set; }

    public string? SecurityPolicy { get; set; }

    public string? CleaningPolicy { get; set; }

    public string? GuestPolicy { get; set; }

    public string? ParkingPolicy { get; set; }

    public string? UtilityPolicy { get; set; }

    public string? DamageCompensationPolicy { get; set; }

    public string? AdditionalNotes { get; set; }
}
