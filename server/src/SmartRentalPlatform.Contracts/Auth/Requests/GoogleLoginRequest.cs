namespace SmartRentalPlatform.Contracts.Auth.Requests;

public class GoogleLoginRequest
{
    public string IdToken { get; set; } = default!;
}
