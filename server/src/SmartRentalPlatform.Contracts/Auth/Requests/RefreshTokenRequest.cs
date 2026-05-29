namespace SmartRentalPlatform.Contracts.Auth.Requests;

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = default!;
}
