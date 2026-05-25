namespace SmartRentalPlatform.Contracts.Users;

public class UserProfileResponse
{
    public Guid UserId { get; set; }
    public string? FullName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? AddressLine { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? VerifiedCitizenIdMasked { get; set; }
    public bool ProfileCompleted { get; set; }
}
