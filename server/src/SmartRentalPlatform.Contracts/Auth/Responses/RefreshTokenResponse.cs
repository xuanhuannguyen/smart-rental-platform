namespace SmartRentalPlatform.Contracts.Auth.Responses;

public class RefreshTokenResponse
{
    public string AccessToken { get; set; } = default!;

    public string RefreshToken { get; set; } = default!;
}
