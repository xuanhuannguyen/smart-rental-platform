namespace SmartRentalPlatform.Contracts.Auth;

public class ResetPasswordRequest
{
    public string Email { get; set; } = default!;

    public string Otp { get; set; } = default!;

    public string NewPassword { get; set; } = default!;
}
