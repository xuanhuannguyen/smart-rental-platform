namespace SmartRentalPlatform.Contracts.Auth.Requests;

public class VerifyEmailOtpRequest
{
	public string Email { get; set; } = default!;

	public string Otp { get; set; } = default!;
}