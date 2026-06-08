using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Payments;

namespace SmartRentalPlatform.Domain.Entities.Payments;

public class PaymentTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WalletAccountId { get; set; }
    public Guid PayerUserId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public PaymentPurpose PaymentPurpose { get; set; } = PaymentPurpose.WalletTopUp;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.PayOS;
    public string ProviderOrderCode { get; set; } = string.Empty;
    public string? ProviderTransactionCode { get; set; }
    public string? ProviderCheckoutUrl { get; set; }
    public string? ProviderQrCode { get; set; }
    public string? GatewayResponseCode { get; set; }
    public string? GatewayResponseMessage { get; set; }
    public PaymentTransactionStatus Status { get; set; } = PaymentTransactionStatus.Pending;
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public WalletAccount WalletAccount { get; set; } = null!;
    public User PayerUser { get; set; } = null!;
    public ICollection<PaymentWebhookLog> WebhookLogs { get; set; } = new List<PaymentWebhookLog>();
}
