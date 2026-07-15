namespace SmartRentalPlatform.Infrastructure.Options;

public sealed class MeterAiOptions
{
    public const string SectionName = "MeterAi";
    public string BaseUrl { get; set; } = "http://127.0.0.1:8001";
    public int TimeoutSeconds { get; set; } = 45;
}
