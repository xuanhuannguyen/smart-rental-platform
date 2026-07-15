namespace SmartRentalPlatform.Domain.Entities.Payments;

public class WithdrawalWebhookLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WithdrawalRequestId { get; set; }
    public string ProviderOrderCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}
