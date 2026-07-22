namespace SmartRentalPlatform.Contracts.Auth.Responses;

public class GoogleLoginResponse
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string? AvatarUrl { get; set; }

    public Guid? AvatarMediaAssetId { get; set; }

    public bool IsGoogleUser { get; set; } = true;

    public bool EmailConfirmed { get; set; }

    public bool RequiresEmailVerification { get; set; }

    public string Status { get; set; } = default!;

    public string OnboardingStatus { get; set; } = default!;

    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();

    public string? AccessToken { get; set; }

    public string? RefreshToken { get; set; }
}
