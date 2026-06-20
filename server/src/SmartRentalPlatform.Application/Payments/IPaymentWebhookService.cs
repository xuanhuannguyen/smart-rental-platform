using SmartRentalPlatform.Contracts.Payments.Responses;

namespace SmartRentalPlatform.Application.Payments;

public interface IPaymentWebhookService
{
    Task<PaymentWebhookProcessingResponse> ProcessPayOSWebhookAsync(
        string rawPayload,
        string? signatureHeader,
        CancellationToken cancellationToken = default);

    Task<PaymentWebhookProcessingResponse> ProcessMockWebhookAsync(
        string rawPayload,
        CancellationToken cancellationToken = default);
}
