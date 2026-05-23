using SmartRentalPlatform.Domain.Enums;
namespace SmartRentalPlatform.Domain.Entities;
public class Role
{
    public short Id { get; set; }
    public RoleName Name { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}