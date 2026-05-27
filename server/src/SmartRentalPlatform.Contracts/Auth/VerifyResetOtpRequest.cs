namespace SmartRentalPlatform.Contracts.Auth;

public class VerifyResetOtpRequest
{
    public string Email { get; set; } = default!;
    public string Otp { get; set; } = default!;
}
