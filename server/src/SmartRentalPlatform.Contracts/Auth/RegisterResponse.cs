namespace SmartRentalPlatform.Contracts.Auth;

public class RegisterResponse
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = default!;

    public bool EmailConfirmed { get; set; }

    public string Status { get; set; } = default!;

    public string OnboardingStatus { get; set; } = default!;

    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
}