namespace SmartRentalPlatform.Contracts.Auth;

public class ResendEmailOtpResponse
{
    public string Email { get; set; } = default!;

    public bool EmailConfirmed { get; set; }

    public bool OtpSent { get; set; }
}