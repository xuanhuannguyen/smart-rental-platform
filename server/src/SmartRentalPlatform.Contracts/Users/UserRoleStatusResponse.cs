using System;
using System.Collections.Generic;

namespace SmartRentalPlatform.Contracts.Users;

public class UserRoleStatusResponse
{
    public Guid UserId { get; set; }
    
    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
    
    public bool IsTenant { get; set; }
    
    public bool IsLandlord { get; set; }
    
    public bool IsAdmin { get; set; }
    
    public string OnboardingStatus { get; set; } = string.Empty;
    
    public string KycStatus { get; set; } = "None"; // None, Pending, Approved, Rejected
    
    public string? KycRejectReason { get; set; }
    
    public string LandlordApplicationStatus { get; set; } = "None"; // None, Pending, Approved, Rejected
    
    public string? LandlordApplicationRejectReason { get; set; }
}
