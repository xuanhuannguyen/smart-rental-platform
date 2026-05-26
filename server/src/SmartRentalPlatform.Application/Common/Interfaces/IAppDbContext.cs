using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Domain.Entities.Administrative;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Users;

namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface IAppDbContext {
    DbSet<User> Users { get; }

    DbSet<Role> Roles { get; }

    DbSet<UserRole> UserRoles { get; }

    DbSet<UserToken> UserTokens { get; }

    DbSet<LoginLog> LoginLogs { get; }

    DbSet<ExternalLogin> ExternalLogins { get; }
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

    Task<IAppDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    DbSet<UserProfile> UserProfiles { get; }

    DbSet<KycVerification> KycVerifications { get; }


    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
