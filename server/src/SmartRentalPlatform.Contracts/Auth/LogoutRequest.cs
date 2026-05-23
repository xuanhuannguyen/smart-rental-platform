namespace SmartRentalPlatform.Contracts.Auth;

public class LogoutRequest
{
    public string RefreshToken { get; set; } = default!;
}
