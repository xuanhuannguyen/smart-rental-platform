namespace SmartRentalPlatform.Contracts.Auth.Responses;

public class ResendEmailOtpResponse
{
    public string Email { get; set; } = default!;

    public bool EmailConfirmed { get; set; }

    public bool OtpSent { get; set; }
}