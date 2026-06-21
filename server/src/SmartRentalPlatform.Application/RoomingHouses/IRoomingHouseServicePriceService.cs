using SmartRentalPlatform.Contracts.RoomingHouses.Requests;
using SmartRentalPlatform.Contracts.RoomingHouses.Responses;

namespace SmartRentalPlatform.Application.RoomingHouses;

public interface IRoomingHouseServicePriceService
{
    Task<List<ServicePriceResponse>> GetServicePricesAsync(Guid landlordUserId, Guid roomingHouseId, CancellationToken cancellationToken = default);
    Task<ServicePriceResponse> CreateServicePriceAsync(Guid landlordUserId, Guid roomingHouseId, CreateServicePriceRequest request, CancellationToken cancellationToken = default);
}
