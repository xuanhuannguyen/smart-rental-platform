namespace SmartRentalPlatform.Contracts.Auth.Responses;

public class ResetPasswordResponse
{
    public string Email { get; set; } = default!;

    public bool EmailConfirmed { get; set; }

    public int RevokedRefreshTokenCount { get; set; }
}
