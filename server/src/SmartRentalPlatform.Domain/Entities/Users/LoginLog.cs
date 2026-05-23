using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Domain.Entities.Users;

public class LoginLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public string EmailAttempted { get; set; } = string.Empty;
    public LoginProvider LoginProvider { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsSuccess { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
}
