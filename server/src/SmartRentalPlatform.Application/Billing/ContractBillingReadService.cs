using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Domain.Enums.Billing;

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
        return await context.Contracts
            .AsNoTracking()
            .Where(x => x.Id == contractId && x.Status == ContractStatus.Active)
            .Where(x => x.Room.Status == RoomStatus.Occupied)
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
                x.Status.ToString()))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
