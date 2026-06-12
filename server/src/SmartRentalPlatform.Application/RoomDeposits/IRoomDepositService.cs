using SmartRentalPlatform.Contracts.RentalRequests.Responses;

namespace SmartRentalPlatform.Application.RoomDeposits;

public interface IRoomDepositService
{
    Task<RoomDepositResponse?> MarkPaidAsync(
        Guid tenantUserId,
        Guid roomDepositId,
        CancellationToken cancellationToken = default);

    Task<int> ExpireOverdueAsync(CancellationToken cancellationToken = default);
}
