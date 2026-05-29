namespace SmartRentalPlatform.Contracts.Auth.Requests;

public class LogoutRequest
{
    public string RefreshToken { get; set; } = default!;
}
