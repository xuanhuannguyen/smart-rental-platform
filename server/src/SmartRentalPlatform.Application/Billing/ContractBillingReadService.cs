using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.Billing;

public class ContractBillingReadService : IBillingContractReadService
{
    private readonly IAppDbContext context;

    public ContractBillingReadService(IAppDbContext context)
    {
        this.context = context;
    }

    public async Task<BillingContractSnapshot?> GetActiveContractAsync(
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        return await context.RentalContracts
            .AsNoTracking()
            .Where(x => x.Id == contractId && x.Status == RentalContractStatus.Active)
            .Where(x => x.Room.Status == RoomStatus.Occupied || x.Room.Status == RoomStatus.Reserved)
            .Select(x => new BillingContractSnapshot(
                x.Id,
                x.RoomId,
                x.Room.RoomingHouseId,
                x.MainTenantUserId,
                x.Room.RoomingHouse.LandlordUserId,
                x.MonthlyRent,
                x.PaymentDay,
                x.StartDate,
                x.EndDate,
                x.Status,
                x.TerminationDate,
                x.TerminationType))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<BillingContractSnapshot?> GetContractAsync(
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        return await context.RentalContracts
            .AsNoTracking()
            .Where(x => x.Id == contractId &&
                        x.DeletedAt == null &&
                        x.Room.DeletedAt == null &&
                        x.Room.RoomingHouse.DeletedAt == null)
            .Select(x => new BillingContractSnapshot(
                x.Id,
                x.RoomId,
                x.Room.RoomingHouseId,
                x.MainTenantUserId,
                x.Room.RoomingHouse.LandlordUserId,
                x.MonthlyRent,
                x.PaymentDay,
                x.StartDate,
                x.EndDate,
                x.Status,
                x.TerminationDate,
                x.TerminationType))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
