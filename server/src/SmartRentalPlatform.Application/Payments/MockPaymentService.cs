using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Payments.Requests;
using SmartRentalPlatform.Contracts.Payments.Responses;

namespace SmartRentalPlatform.Application.Payments;

public class MockPaymentService : IMockPaymentService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IAppDbContext context;
    private readonly IPaymentWebhookService paymentWebhookService;

    public MockPaymentService(IAppDbContext context, IPaymentWebhookService paymentWebhookService)
    {
        this.context = context;
        this.paymentWebhookService = paymentWebhookService;
    }

    public async Task<PaymentWebhookProcessingResponse> SimulateSuccessAsync(
        Guid paymentTransactionId,
        MockPaymentRequest? request,
        CancellationToken cancellationToken = default)
    {
        var rawPayload = await BuildMockPayloadAsync(paymentTransactionId, "Succeeded", request, cancellationToken);
        return await paymentWebhookService.ProcessMockWebhookAsync(rawPayload, cancellationToken);
    }

    public async Task<PaymentWebhookProcessingResponse> SimulateFailedAsync(
        Guid paymentTransactionId,
        MockPaymentRequest? request,
        CancellationToken cancellationToken = default)
    {
        var rawPayload = await BuildMockPayloadAsync(paymentTransactionId, "Failed", request, cancellationToken);
        return await paymentWebhookService.ProcessMockWebhookAsync(rawPayload, cancellationToken);
    }

    private async Task<string> BuildMockPayloadAsync(
        Guid paymentTransactionId,
        string status,
        MockPaymentRequest? request,
        CancellationToken cancellationToken)
    {
        var paymentTransaction = await context.PaymentTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == paymentTransactionId, cancellationToken);

        var payload = new
        {
            providerEventId = $"mock-{paymentTransactionId:N}-{status.ToLowerInvariant()}",
            providerOrderCode = paymentTransaction?.ProviderOrderCode,
            providerTransactionCode = $"mock-txn-{paymentTransactionId:N}",
            idempotencyKey = paymentTransaction?.IdempotencyKey ?? paymentTransactionId.ToString("N"),
            amount = request?.Amount ?? paymentTransaction?.Amount ?? 0m,
            status,
            success = status == "Succeeded",
            code = status == "Succeeded" ? "00" : "FAILED",
            desc = status == "Succeeded" ? "Mock payment succeeded." : "Mock payment failed."
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}
