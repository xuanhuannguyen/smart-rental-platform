using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Domain.Entities;
using SmartRentalPlatform.Infrastructure.Persistence.Seed;
using SmartRentalPlatform.Application.Common.Interfaces;


namespace SmartRentalPlatform.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();
    public DbSet<LoginLog> LoginLogs => Set<LoginLog>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        RoleSeed.Seed(modelBuilder);
    }
}
