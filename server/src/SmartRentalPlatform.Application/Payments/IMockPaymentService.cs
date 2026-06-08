using SmartRentalPlatform.Contracts.Payments.Requests;
using SmartRentalPlatform.Contracts.Payments.Responses;

namespace SmartRentalPlatform.Application.Payments;

public interface IMockPaymentService
{
    Task<PaymentWebhookProcessingResponse> SimulateSuccessAsync(
        Guid paymentTransactionId,
        MockPaymentRequest? request,
        CancellationToken cancellationToken = default);

    Task<PaymentWebhookProcessingResponse> SimulateFailedAsync(
        Guid paymentTransactionId,
        MockPaymentRequest? request,
        CancellationToken cancellationToken = default);
}
