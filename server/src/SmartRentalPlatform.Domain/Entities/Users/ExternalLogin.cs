using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Domain.Entities.Users;

public class ExternalLogin
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public LoginProvider Provider { get; set; }
    public string ProviderUserId { get; set; } = string.Empty;
    public string ProviderEmail { get; set; } = string.Empty;
    public string? ProviderDisplayName { get; set; }
    public string? ProviderAvatarUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }

    public User User { get; set; } = null!;
}
