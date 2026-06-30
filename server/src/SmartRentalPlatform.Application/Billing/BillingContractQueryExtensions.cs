using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.Billing;

internal static class BillingContractQueryExtensions
{
    public static IQueryable<RentalContract> WhereActiveForOccupiedOrReservedRoom(
        this IQueryable<RentalContract> query)
    {
        return query.Where(x =>
            x.DeletedAt == null &&
            x.Status == RentalContractStatus.Active &&
            (x.Room.Status == RoomStatus.Occupied ||
             x.Room.Status == RoomStatus.Reserved) &&
            x.Room.DeletedAt == null &&
            x.Room.RoomingHouse.DeletedAt == null);
    }
}
