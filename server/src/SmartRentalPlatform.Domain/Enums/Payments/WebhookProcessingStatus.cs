namespace SmartRentalPlatform.Domain.Enums.Payments;

public enum WebhookProcessingStatus
{
    Received,
    Processed,
    Duplicate,
    Failed,
    Unmatched
}
