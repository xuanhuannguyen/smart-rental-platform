namespace SmartRentalPlatform.Contracts.Auth;

public class ResetPasswordResponse
{
    public string Email { get; set; } = default!;

    public bool EmailConfirmed { get; set; }

    public int RevokedRefreshTokenCount { get; set; }
}
