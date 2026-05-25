using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.Amenities;

public interface IAmenityService
{
    Task<List<AmenityResponse>> GetActiveAmenitiesAsync(
        AmenityScope? scope,
        CancellationToken cancellationToken = default);
}
