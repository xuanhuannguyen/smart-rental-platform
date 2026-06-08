namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface IPayOSClient
{
    Task<PayOSCreatePaymentResult> CreatePaymentAsync(
        PayOSCreatePaymentInput input,
        CancellationToken cancellationToken = default);
}

public sealed class PayOSCreatePaymentInput
{
    public Guid PaymentTransactionId { get; set; }
    public Guid PayerUserId { get; set; }
    public string ProviderOrderCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Description { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
    public string? CancelUrl { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class PayOSCreatePaymentResult
{
    public string? ProviderTransactionCode { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? QrCode { get; set; }
    public string? GatewayResponseCode { get; set; }
    public string? GatewayResponseMessage { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
