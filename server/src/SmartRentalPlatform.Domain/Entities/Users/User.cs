using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Rental;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Domain.Entities.Users;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? PasswordHash { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public Guid? AvatarMediaAssetId { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public OnboardingStatus OnboardingStatus { get; set; } = OnboardingStatus.NeedProfileUpdate;
    public bool EmailConfirmed { get; set; }
    public bool PhoneConfirmed { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTimeOffset? LockoutEndAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public MediaAsset? AvatarMediaAsset { get; set; }
    public UserProfile? UserProfile { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<ExternalLogin> ExternalLogins { get; set; } = new List<ExternalLogin>();
    public ICollection<UserToken> UserTokens { get; set; } = new List<UserToken>();
    public ICollection<LoginLog> LoginLogs { get; set; } = new List<LoginLog>();
    public ICollection<KycVerification> KycVerifications { get; set; } = new List<KycVerification>();
    public ICollection<RoomingHouse> RoomingHouses { get; set; } = new List<RoomingHouse>();
    public ICollection<RoomingHouse> ReviewedRoomingHouses { get; set; } = new List<RoomingHouse>();
    public ICollection<ViewingAppointment> TenantAppointments { get; set; } = new List<ViewingAppointment>();
    public ICollection<ViewingAppointment> CreatedAppointments { get; set; } = new List<ViewingAppointment>();
    public ICollection<RentalRequest> RentalRequests { get; set; } = new List<RentalRequest>();
}
