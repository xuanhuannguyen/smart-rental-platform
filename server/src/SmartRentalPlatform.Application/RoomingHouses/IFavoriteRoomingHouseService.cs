using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RoomingHouses.Responses;

namespace SmartRentalPlatform.Application.RoomingHouses;

public interface IFavoriteRoomingHouseService
{
    Task<bool> ToggleFavoriteAsync(Guid roomingHouseId, Guid currentUserId, CancellationToken cancellationToken = default);
    
    Task<PagedResult<RoomingHouseListingResponse>> GetMyFavoritesAsync(Guid currentUserId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    Task<List<Guid>> GetMyFavoriteIdsAsync(Guid currentUserId, CancellationToken cancellationToken = default);
}
