namespace SmartRentalPlatform.Contracts.Auth.Requests;

public class RegisterRequest
{
    public string Email { get; set; } = default!;

    public string Password { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string? PhoneNumber { get; set; }
}