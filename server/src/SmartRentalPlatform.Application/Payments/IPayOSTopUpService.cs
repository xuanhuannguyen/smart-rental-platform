using SmartRentalPlatform.Contracts.Wallets.Requests;
using SmartRentalPlatform.Contracts.Wallets.Responses;

namespace SmartRentalPlatform.Application.Payments;

public interface IPayOSTopUpService
{
    Task<CreatePayOSTopUpResponse> CreateTopUpAsync(
        Guid userId,
        CreatePayOSTopUpRequest request,
        CancellationToken cancellationToken = default);
}
