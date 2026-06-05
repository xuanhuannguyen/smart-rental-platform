using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Domain.Entities.Administrative;
using SmartRentalPlatform.Domain.Entities.AdminApproval;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Leasing;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Entities.Wallets;


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
    public DbSet<LeasePolicy> LeasePolicies => Set<LeasePolicy>();
    public DbSet<ApprovalAuditLog> ApprovalAuditLogs => Set<ApprovalAuditLog>();
    // Leasing minimal read model
    public DbSet<Contract> Contracts => Set<Contract>();
    // Wallet minimal read model
    public DbSet<WalletAccount> WalletAccounts => Set<WalletAccount>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    // Billing
    public DbSet<BillingServiceType> BillingServiceTypes => Set<BillingServiceType>();
    public DbSet<RoomingHouseServicePrice> RoomingHouseServicePrices => Set<RoomingHouseServicePrice>();
    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<InvoicePayment> InvoicePayments => Set<InvoicePayment>();

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
