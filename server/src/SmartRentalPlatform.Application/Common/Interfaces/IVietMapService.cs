using SmartRentalPlatform.Contracts.Locations;

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
}
