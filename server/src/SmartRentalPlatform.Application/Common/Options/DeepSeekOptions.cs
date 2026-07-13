namespace SmartRentalPlatform.Application.Common.Options;

public sealed class DeepSeekOptions
{
    public const string SectionName = "DeepSeek";

    public bool Enabled { get; set; } = true;

    public string BaseUrl { get; set; } = "https://api.deepseek.com";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "deepseek-v4-flash";

    public int TimeoutSeconds { get; set; } = 30;

    public int MaxTokens { get; set; } = 1600;

    public decimal Temperature { get; set; } = 0.2m;

    public bool HasCredential()
        => Enabled && !string.IsNullOrWhiteSpace(ApiKey);
}
