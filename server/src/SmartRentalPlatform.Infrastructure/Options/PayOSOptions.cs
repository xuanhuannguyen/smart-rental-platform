namespace SmartRentalPlatform.Infrastructure.Options;

public class PayOSOptions
{
    public const string SectionName = "PayOS";

    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChecksumKey { get; set; } = string.Empty;
    public string PayoutClientId { get; set; } = string.Empty;
    public string PayoutApiKey { get; set; } = string.Empty;
    public string PayoutChecksumKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api-merchant.payos.vn";
    public string ReturnUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
    public string CreatePaymentPath { get; set; } = "v2/payment-requests";
    public int TimeoutSeconds { get; set; } = 30;
}
