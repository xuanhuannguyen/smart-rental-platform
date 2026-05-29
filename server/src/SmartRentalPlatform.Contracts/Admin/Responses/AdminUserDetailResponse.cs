using System;
using System.Collections.Generic;

namespace SmartRentalPlatform.Contracts.Admin.Responses;

public class AdminUserDetailResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public List<string> Roles { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public string OnboardingStatus { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string? FullName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? AddressLine { get; set; }
    public string? VerifiedCitizenIdMasked { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public AdminKycInfo? KycInfo { get; set; }
}
