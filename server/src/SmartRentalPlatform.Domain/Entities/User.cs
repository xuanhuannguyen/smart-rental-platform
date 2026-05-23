using SmartRentalPlatform.Domain.Enums;
namespace SmartRentalPlatform.Domain.Entities;
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = default!;
    public string NormalizedEmail { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public string? PasswordHash { get; set; }
    public string DisplayName { get; set; } = default!;
    public string? AvatarUrl { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public OnboardingStatus OnboardingStatus { get; set; } =
        OnboardingStatus.NeedProfileUpdate;
    public bool EmailConfirmed { get; set; } = false;
    public bool PhoneConfirmed { get; set; } = false;
    public int AccessFailedCount { get; set; } = 0;
    public DateTimeOffset? LockoutEndAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    // 1 user - nhiều role qua bảng trung gian user_roles
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    // 1 user - nhiều external login
    public ICollection<ExternalLogin> ExternalLogins { get; set; } = new List<ExternalLogin>();
    // 1 user - nhiều token
    public ICollection<UserToken> UserTokens { get; set; } = new List<UserToken>();
    // 1 user - nhiều login log
    public ICollection<LoginLog> LoginLogs { get; set; } = new List<LoginLog>();
}