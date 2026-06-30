namespace SmartRentalPlatform.Application.Common.Options;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gemini-2.5-flash";

    public int TimeoutSeconds { get; set; } = 8;

    public bool Enabled { get; set; } = true;

    public bool UseAiSearchFallback { get; set; } = true;

    public bool UseAiGuestRecommendations { get; set; } = true;
}
