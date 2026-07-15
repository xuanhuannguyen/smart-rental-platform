using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Domain.Entities.RentalContracts;

public class ESignWebhookLog
{
    public Guid Id { get; set; }
    
    public ESignProvider Provider { get; set; }
    
    public Guid? SigningEnvelopeId { get; set; }
    public string? ProviderEventId { get; set; }
    public string? ProviderEnvelopeId { get; set; }
    public string? IdempotencyKey { get; set; }
    public string RawPayload { get; set; } = string.Empty;
    public string RawPayloadHash { get; set; } = string.Empty;
    public WebhookSignatureStatus SignatureStatus { get; set; }
    public ESignWebhookProcessingStatus ProcessingStatus { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
