using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Domain.Entities.Administrative;
using SmartRentalPlatform.Domain.Entities.AdminApproval;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Entities.Properties;

namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface IAppDbContext {
    DbSet<User> Users { get; }

    DbSet<Role> Roles { get; }

    DbSet<UserRole> UserRoles { get; }

    DbSet<UserToken> UserTokens { get; }

    DbSet<LoginLog> LoginLogs { get; }

    DbSet<ExternalLogin> ExternalLogins { get; }

    DbSet<UserProfile> UserProfiles { get; }

    DbSet<KycVerification> KycVerifications { get; }

    DbSet<AdministrativeProvince> AdministrativeProvinces { get; }

    DbSet<AdministrativeWard> AdministrativeWards { get; }

    DbSet<RoomingHouse> RoomingHouses { get; }

    DbSet<Room> Rooms { get; }

    DbSet<Amenity> Amenities { get; }

    DbSet<PropertyImage> PropertyImages { get; }

    DbSet<RoomingHouseLegalDocument> RoomingHouseLegalDocuments { get; }

    DbSet<RoomingHouseAmenity> RoomingHouseAmenities { get; }

    DbSet<RoomAmenity> RoomAmenities { get; }

    DbSet<RoomPriceTier> RoomPriceTiers { get; }

    DbSet<LeasePolicy> LeasePolicies { get; }

    DbSet<ApprovalAuditLog> ApprovalAuditLogs { get; }

    DbSet<ViewingAppointment> ViewingAppointments { get; }

    Task<IAppDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
