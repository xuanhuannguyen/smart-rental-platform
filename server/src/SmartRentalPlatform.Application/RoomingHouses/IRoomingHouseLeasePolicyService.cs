using SmartRentalPlatform.Contracts.LeasePolicies;

namespace SmartRentalPlatform.Application.RoomingHouses;

public interface IRoomingHouseLeasePolicyService
{
    Task<LeasePolicyResponse?> GetLeasePolicyAsync(
        Guid roomingHouseId,
        CancellationToken cancellationToken = default);

    Task<LeasePolicyResponse> UpdateLeasePolicyAsync(
        Guid roomingHouseId,
        Guid landlordUserId,
        UpdateLeasePolicyRequest request,
        CancellationToken cancellationToken = default);
}
