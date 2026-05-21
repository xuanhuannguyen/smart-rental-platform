using SmartRentalPlatform.Domain.Entities;
using SmartRentalPlatfrom.Domain.Enums;
namespace SmartRentalPlatfrom.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? PasswordHash { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? AvatarSource { get; set; }
    public string? AvatarObjectKey { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public OnboardingStatus OnboardingStatus { get; set; } = OnboardingStatus.NeedRoleSelection;

    public bool EmailComfirmed { get; set; }
    public bool PhoneConfirmed { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTime? LockoutEndAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public UserProfile? UserProfile { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();


}