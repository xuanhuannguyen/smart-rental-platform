using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Wallets.Requests;
using SmartRentalPlatform.Contracts.Wallets.Responses;

namespace SmartRentalPlatform.Application.Payments;

public interface IPayOSTopUpService
{
    Task<CreatePayOSTopUpResponse> CreateTopUpAsync(
        Guid userId,
        CreatePayOSTopUpRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<WalletTopUpHistoryResponse>> GetTopUpHistoryAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
