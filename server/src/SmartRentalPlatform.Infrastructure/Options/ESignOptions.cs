namespace SmartRentalPlatform.Infrastructure.Options;

public class ESignOptions
{
    public const string SectionName = "ESign";

    public string Provider { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string ProductionBaseUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int NenTangId { get; set; } = 1;
    public string LoaiTaiLieuId { get; set; } = string.Empty;
    public string WebhookCallbackKey { get; set; } = string.Empty;
    public string WebhookCallbackSecret { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public int TokenCacheDurationMinutes { get; set; } = 50;
    public int TimeoutSeconds { get; set; } = 30;
}
