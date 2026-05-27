namespace SmartRentalPlatform.Contracts.Auth;

public class ChangePasswordResponse
{
    public bool PasswordChanged { get; set; }

    public int RevokedRefreshTokenCount { get; set; }
}
