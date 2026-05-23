using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Domain.Entities.Users;

public class UserToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public UserTokenType TokenType { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public Guid TokenFamilyId { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public TokenRevokedReason? RevokedReason { get; set; }
    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public UserToken? ReplacedByToken { get; set; }
    public ICollection<UserToken> ReplacingTokens { get; set; } = new List<UserToken>();
}
