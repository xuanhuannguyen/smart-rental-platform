using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Domain.Entities.AdminApproval;
using SmartRentalPlatform.Domain.Entities.Administrative;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Rental;
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
    public DbSet<RentalPolicy> RentalPolicies => Set<RentalPolicy>();
    public DbSet<ApprovalAuditLog> ApprovalAuditLogs => Set<ApprovalAuditLog>();
    // Rental
    public DbSet<RentalRequest> RentalRequests => Set<RentalRequest>();
    public DbSet<RoomDeposit> RoomDeposits => Set<RoomDeposit>();
    // Contracts
    public DbSet<RentalContract> RentalContracts => Set<RentalContract>();
    public DbSet<ContractOccupant> ContractOccupants => Set<ContractOccupant>();
    public DbSet<ContractOccupantDocument> ContractOccupantDocuments => Set<ContractOccupantDocument>();
    public DbSet<ContractAppendix> ContractAppendices => Set<ContractAppendix>();
    public DbSet<ContractAppendixChange> ContractAppendixChanges => Set<ContractAppendixChange>();
    public DbSet<ContractFile> ContractFiles => Set<ContractFile>();
    public DbSet<ContractSignature> ContractSignatures => Set<ContractSignature>();

    public async Task<IAppDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
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
