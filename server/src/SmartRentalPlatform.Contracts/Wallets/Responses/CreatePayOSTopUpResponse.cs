namespace SmartRentalPlatform.Contracts.Wallets.Responses;

public class CreatePayOSTopUpResponse
{
    public Guid PaymentTransactionId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ProviderOrderCode { get; set; } = string.Empty;
    public string? PaymentUrl { get; set; }
    public string? QrCode { get; set; }
    public DateTimeOffset ExpiredAt { get; set; }
}
