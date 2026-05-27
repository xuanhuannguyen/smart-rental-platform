using System;
using System.Collections.Generic;

namespace SmartRentalPlatform.Contracts.Admin;

public class AdminUserListItemResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public List<string> Roles { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public string OnboardingStatus { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
