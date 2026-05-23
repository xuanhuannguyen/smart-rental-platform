namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface IGoogleAuthService
{
    Task<GoogleUserInfo> VerifyIdTokenAsync(
        string idToken,
        CancellationToken cancellationToken = default);
}

public sealed class GoogleUserInfo
{
    public string ProviderUserId { get; set; } = default!;

    public string Email { get; set; } = default!;

    public string? DisplayName { get; set; }

    public string? AvatarUrl { get; set; }

    public bool EmailVerified { get; set; }
}
