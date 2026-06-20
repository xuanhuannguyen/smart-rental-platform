namespace SmartRentalPlatform.Contracts.Payments.Responses;

public class PaymentWebhookProcessingResponse
{
    public Guid? PaymentTransactionId { get; set; }
    public Guid? WebhookLogId { get; set; }
    public string ProcessingStatus { get; set; } = string.Empty;
    public string SignatureStatus { get; set; } = string.Empty;
    public string? PaymentStatus { get; set; }
    public string? ProviderOrderCode { get; set; }
    public string? Message { get; set; }
}
