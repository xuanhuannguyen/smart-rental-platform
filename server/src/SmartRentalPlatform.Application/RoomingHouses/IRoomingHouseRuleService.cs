using SmartRentalPlatform.Contracts.RoomingHouseRules.Requests;
using SmartRentalPlatform.Contracts.RoomingHouseRules.Responses;

namespace SmartRentalPlatform.Application.RoomingHouses;

public interface IRoomingHouseRuleService
{
    Task<RoomingHouseRuleResponse?> GetRuleAsync(
        Guid roomingHouseId,
        Guid landlordUserId,
        CancellationToken cancellationToken = default);

    Task<RoomingHouseRuleResponse> UpsertRuleAsync(
        Guid roomingHouseId,
        Guid landlordUserId,
        UpsertRoomingHouseRuleRequest request,
        CancellationToken cancellationToken = default);
}
