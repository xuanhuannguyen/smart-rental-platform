using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Domain.Entities.Administrative;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Users;

namespace SmartRentalPlatform.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // Users
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();
    public DbSet<LoginLog> LoginLogs => Set<LoginLog>();
    public DbSet<KycVerification> KycVerifications => Set<KycVerification>();

    // Administrative
    public DbSet<AdministrativeProvince> AdministrativeProvinces => Set<AdministrativeProvince>();
    public DbSet<AdministrativeWard> AdministrativeWards => Set<AdministrativeWard>();

    // Properties
    public DbSet<RoomingHouse> RoomingHouses => Set<RoomingHouse>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomPriceTier> RoomPriceTiers => Set<RoomPriceTier>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<RoomingHouseAmenity> RoomingHouseAmenities => Set<RoomingHouseAmenity>();
    public DbSet<RoomAmenity> RoomAmenities => Set<RoomAmenity>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<RoomingHouseLegalDocument> RoomingHouseLegalDocuments => Set<RoomingHouseLegalDocument>();

    public async Task<IAppDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(cancellationToken);
        return new AppDbContextTransaction(transaction);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}