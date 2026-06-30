namespace SmartRentalPlatform.Contracts.Users;

public class OccupantAccountLookupResponse
{
    public string Email { get; set; } = string.Empty;

    public bool Exists { get; set; }

    public bool IsKycApproved { get; set; }

    public string? DisplayName { get; set; }
}
