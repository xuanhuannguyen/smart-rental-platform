using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Domain.Entities.AdminApproval;
using SmartRentalPlatform.Domain.Entities.Administrative;
using SmartRentalPlatform.Domain.Entities.Media;

using SmartRentalPlatform.Domain.Entities.Notifications;
using SmartRentalPlatform.Domain.Entities.Payments;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Chat;
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
    public DbSet<FavoriteRoomingHouse> FavoriteRoomingHouses => Set<FavoriteRoomingHouse>();
    public DbSet<RoomingHouseReview> RoomingHouseReviews => Set<RoomingHouseReview>();
    public DbSet<ReviewReport> ReviewReports => Set<ReviewReport>();
    public DbSet<RoomingHouseLegalDocument> RoomingHouseLegalDocuments => Set<RoomingHouseLegalDocument>();
    public DbSet<RoomingHouseRule> RoomingHouseRules => Set<RoomingHouseRule>();
    public DbSet<RentalPolicy> RentalPolicies => Set<RentalPolicy>();
    public DbSet<ApprovalAuditLog> ApprovalAuditLogs => Set<ApprovalAuditLog>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<MediaAuditLog> MediaAuditLogs => Set<MediaAuditLog>();
    // Payments
    public DbSet<WalletAccount> WalletAccounts => Set<WalletAccount>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<PaymentWebhookLog> PaymentWebhookLogs => Set<PaymentWebhookLog>();
    public DbSet<SmartRentalPlatform.Domain.Entities.Payments.WithdrawalRequest> WithdrawalRequests => Set<SmartRentalPlatform.Domain.Entities.Payments.WithdrawalRequest>();
    public DbSet<SmartRentalPlatform.Domain.Entities.Payments.WithdrawalWebhookLog> WithdrawalWebhookLogs => Set<SmartRentalPlatform.Domain.Entities.Payments.WithdrawalWebhookLog>();
    // Billing
    public DbSet<BillingServiceType> BillingServiceTypes => Set<BillingServiceType>();
    public DbSet<RoomingHouseServicePrice> RoomingHouseServicePrices => Set<RoomingHouseServicePrice>();
    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    // Chat
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ConversationJoinRequest> ConversationJoinRequests => Set<ConversationJoinRequest>();

    // Viewing appointments
    public DbSet<ViewingAppointment> ViewingAppointments => Set<ViewingAppointment>();
    // Notifications
    public DbSet<Notification> Notifications => Set<Notification>();
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
    public DbSet<ContractSigningEnvelope> ContractSigningEnvelopes => Set<ContractSigningEnvelope>();
    public DbSet<ESignWebhookLog> ESignWebhookLogs => Set<ESignWebhookLog>();


    public async Task<IAppDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(cancellationToken);
        return new AppDbContextTransaction(transaction);
    }

    public bool HasActiveTransaction => Database.CurrentTransaction != null;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
