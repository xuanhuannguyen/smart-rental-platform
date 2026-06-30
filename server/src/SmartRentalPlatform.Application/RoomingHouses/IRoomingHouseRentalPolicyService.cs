using SmartRentalPlatform.Contracts.RentalPolicies.Requests;
using SmartRentalPlatform.Contracts.RentalPolicies.Responses;

namespace SmartRentalPlatform.Application.RoomingHouses;

public interface IRoomingHouseRentalPolicyService
{
    Task<RentalPolicyResponse?> GetRentalPolicyAsync(
        Guid roomingHouseId,
        CancellationToken cancellationToken = default);

    Task<RentalPolicyResponse> UpdateRentalPolicyAsync(
        Guid roomingHouseId,
        Guid landlordUserId,
        UpdateRentalPolicyRequest request,
        CancellationToken cancellationToken = default);
}
