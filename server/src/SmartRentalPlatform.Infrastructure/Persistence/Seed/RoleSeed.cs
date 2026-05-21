using SmartRentalPlatform.Domain.Entities;
namespace SmartRentalPlatform.Infrastructure.Persistence.Seed;

public static class RoleSeed
{
    public static readonly Guid AdminRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid TenantRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid LandlordRoleId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly DateTime SeededAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static Role[] GetRoles()
    {
        return
        [
            new Role
            {
                Id = AdminRoleId,
                Name = "Admin",
                Description = "System administrator",
                CreatedAt = SeededAt
            }

        ]
    }
}