using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Domain.Entities;

namespace SmartRentalPlatform.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Role => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    public DbSet<KycVerification> KycVerifications => Set<KycVerification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Sau này sẽ tự động load các cấu hình bảng ở folder Configurations.
        // Ví dụ: UserConfiguration, RoleConfiguration, RoomConfiguration...
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}