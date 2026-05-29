namespace SmartRentalPlatform.Contracts.Users.Responses;

public class UserProfileResponse
{
    public Guid UserId { get; set; }
    public string? DisplayName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? FullName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? AddressLine { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? VerifiedCitizenIdMasked { get; set; }
    public string? KycStatus { get; set; }
    public DateTimeOffset? KycReviewedAt { get; set; }
    public bool IdentityVerified { get; set; }
    public bool ProfileCompleted { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsGoogleUser { get; set; }
}
