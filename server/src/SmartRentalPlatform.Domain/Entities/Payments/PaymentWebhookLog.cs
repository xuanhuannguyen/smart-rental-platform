using SmartRentalPlatform.Domain.Enums.Payments;

namespace SmartRentalPlatform.Domain.Entities.Payments;

public class PaymentWebhookLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? PaymentTransactionId { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? ProviderEventId { get; set; }
    public string? ProviderOrderCode { get; set; }
    public string? ProviderTransactionCode { get; set; }
    public string? IdempotencyKey { get; set; }
    public string RawPayload { get; set; } = string.Empty;
    public string RawPayloadHash { get; set; } = string.Empty;
    public WebhookSignatureStatus SignatureStatus { get; set; }
    public WebhookProcessingStatus ProcessingStatus { get; set; } = WebhookProcessingStatus.Received;
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public PaymentTransaction? PaymentTransaction { get; set; }
}
