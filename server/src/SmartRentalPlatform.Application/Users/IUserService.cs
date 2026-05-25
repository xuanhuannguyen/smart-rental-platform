using SmartRentalPlatform.Contracts.Users;

namespace SmartRentalPlatform.Application.Users;

public interface IUserService
{
    Task<CurrentUserResponse> GetCurrentUserAsync(
        CancellationToken cancellationToken = default);

    Task<LandlordEligibilityResponse> GetLandlordEligibilityAsync(
        CancellationToken cancellationToken = default);
}