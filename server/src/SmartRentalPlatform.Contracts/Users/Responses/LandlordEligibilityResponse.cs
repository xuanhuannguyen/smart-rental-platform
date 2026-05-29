namespace SmartRentalPlatform.Contracts.Users.Responses;

public class LandlordEligibilityResponse
{
    public bool CanContinue { get; set; }
    public string NextStep { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
