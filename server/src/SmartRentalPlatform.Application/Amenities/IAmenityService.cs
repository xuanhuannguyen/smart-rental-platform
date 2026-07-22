using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.Amenities.Requests;
using SmartRentalPlatform.Contracts.Amenities.Responses;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.Amenities;

public interface IAmenityService
{
    Task<List<AmenityResponse>> GetActiveAmenitiesAsync(
        AmenityScope? scope,
        CancellationToken cancellationToken = default);

    // Admin CRUD
    Task<PagedResult<AdminAmenityResponse>> GetAmenitiesAsync(int page, int pageSize, string? keyword, CancellationToken cancellationToken = default);
    Task<AdminAmenityResponse> GetAmenityAsync(int id, CancellationToken cancellationToken = default);
    Task<AdminAmenityResponse> CreateAmenityAsync(CreateAmenityRequest request, CancellationToken cancellationToken = default);
    Task<AdminAmenityResponse> UpdateAmenityAsync(int id, UpdateAmenityRequest request, CancellationToken cancellationToken = default);
    Task ToggleAmenityActiveAsync(int id, CancellationToken cancellationToken = default);
}
