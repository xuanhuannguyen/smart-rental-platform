using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.Payments;

namespace SmartRentalPlatform.Application.Wallets;

public interface IWithdrawalService
{
    Task<WithdrawalRequest> RequestWithdrawalAsync(
        Guid userId,
        decimal amount,
        string bankBin,
        string accountNumber,
        string accountName,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<PagedResult<WithdrawalRequest>> GetMyWithdrawalRequestsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
