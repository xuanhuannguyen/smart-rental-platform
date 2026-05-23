using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Infrastructure.Persistence.Seed;

public static class RoleSeed
{
    public const int AdminRoleId = 1;
    public const int TenantRoleId = 2;
    public const int LandlordRoleId = 3;

    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static Role[] GetRoles()
    {
        return
        [
            new Role
            {
                Id = AdminRoleId,
                Name = RoleName.Admin,
                Description = "System administrator",
                CreatedAt = SeededAt
            },
            new Role
            {
                Id = TenantRoleId,
                Name = RoleName.Tenant,
                Description = "Rental tenant",
                CreatedAt = SeededAt
            },
            new Role
            {
                Id = LandlordRoleId,
                Name = RoleName.Landlord,
                Description = "Rooming house landlord",
                CreatedAt = SeededAt
            }
        ];
    }
}
