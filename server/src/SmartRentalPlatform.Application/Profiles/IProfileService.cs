using SmartRentalPlatform.Contracts.Profiles;

namespace SmartRentalPlatform.Application.Profiles;

public interface IProfileService
{
    Task<UserProfileResponse> GetUserProfileAsync(
        CancellationToken cancellationToken = default);

    Task<UserProfileResponse> UpdateUserProfileAsync(
        UpdateUserProfileRequest request,
        CancellationToken cancellationToken = default);
}
