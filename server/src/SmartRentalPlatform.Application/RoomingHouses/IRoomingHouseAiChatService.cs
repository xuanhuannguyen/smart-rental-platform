using SmartRentalPlatform.Contracts.RoomingHouses.Requests;
using SmartRentalPlatform.Contracts.RoomingHouses.Responses;

namespace SmartRentalPlatform.Application.RoomingHouses;

public interface IRoomingHouseAiChatService
{
    Task<RoomingHouseAiChatResponse> ChatAsync(
        RoomingHouseAiChatRequest request,
        CancellationToken cancellationToken = default);
}
