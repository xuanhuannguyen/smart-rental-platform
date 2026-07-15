namespace SmartRentalPlatform.Domain.Enums.RentalContracts;

public enum ESignWebhookProcessingStatus
{
    Pending = 1,
    Processed = 2,
    Failed = 3,
    Duplicate = 4,
    Ignored = 5
}
