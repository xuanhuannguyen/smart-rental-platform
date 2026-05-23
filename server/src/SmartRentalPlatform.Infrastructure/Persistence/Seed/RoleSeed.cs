using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Domain.Entities;
using SmartRentalPlatform.Domain.Enums;
namespace SmartRentalPlatform.Infrastructure.Persistence.Seed;

public static class RoleSeed
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = RoleName.Admin, Description = "Quản trị hệ thống", CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
            new Role { Id = 2, Name = RoleName.Tenant, Description = "Người thuê / người tìm phòng", CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
            new Role { Id = 3, Name = RoleName.Landlord, Description = "Chủ trọ", CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) }
        );
    }
}