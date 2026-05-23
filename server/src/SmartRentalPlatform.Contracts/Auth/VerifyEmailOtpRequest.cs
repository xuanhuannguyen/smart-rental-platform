namespace SmartRentalPlatform.Contracts.Auth;

public class VerifyEmailOtpRequest
{
	public string Email { get; set; } = default!;

	public string Otp { get; set; } = default!;
}