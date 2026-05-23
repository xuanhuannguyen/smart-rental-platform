namespace SmartRentalPlatform.Domain.Entities;
public class UserRole
{
    public Guid UserId { get; set; }
    public short RoleId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public User User { get; set; } = default!;
    public Role Role { get; set; } = default!;
}