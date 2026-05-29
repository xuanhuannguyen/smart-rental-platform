namespace SmartRentalPlatform.Contracts.Auth.Responses;

public class ChangePasswordResponse
{
    public bool PasswordChanged { get; set; }

    public int RevokedRefreshTokenCount { get; set; }
}
