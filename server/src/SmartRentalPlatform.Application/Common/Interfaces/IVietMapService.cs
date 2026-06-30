using SmartRentalPlatform.Contracts.Locations;
using SmartRentalPlatform.Contracts.RoomingHouses.Responses;

namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface IVietMapService
{
    Task<LocationSearchResponse> SearchAddressAsync(
        string text,
        CancellationToken cancellationToken = default);

    Task<List<LocationSuggestionResponse>> SuggestAddressesAsync(
        string text,
        int limit = 5,
        CancellationToken cancellationToken = default);

    Task<List<NearbyPlaceResponse>> SearchNearbyPlacesAsync(
        decimal latitude,
        decimal longitude,
        string keyword,
        int radiusMeters = 1500,
        int limit = 6,
        CancellationToken cancellationToken = default);
}
