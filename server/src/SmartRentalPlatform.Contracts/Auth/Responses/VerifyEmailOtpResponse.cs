namespace SmartRentalPlatform.Contracts.Auth.Responses;

public class VerifyEmailOtpResponse
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = default!;

    public bool EmailConfirmed { get; set; }
}