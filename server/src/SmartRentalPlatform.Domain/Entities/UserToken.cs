using SmartRentalPlatform.Domain.Enums;
namespace SmartRentalPlatform.Domain.Entities;
public class UserToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public TokenType TokenType { get; set; }
    public string TokenHash { get; set; } = default!;
    public Guid? TokenFamilyId { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public TokenRevokedReason? RevokedReason { get; set; }
    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public User User { get; set; } = default!;
}