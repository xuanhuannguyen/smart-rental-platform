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

    public bool UseVertexAi { get; set; } = false;

    public string ProjectId { get; set; } = string.Empty;

    public string Region { get; set; } = "us-central1";

    public string ServiceAccountJson { get; set; } = string.Empty;

    public bool HasCredential()
    {
        if (UseVertexAi)
        {
            return !string.IsNullOrWhiteSpace(ServiceAccountJson)
                && !string.IsNullOrWhiteSpace(ProjectId)
                && !string.IsNullOrWhiteSpace(Region);
        }

        return !string.IsNullOrWhiteSpace(ApiKey);
    }
}

