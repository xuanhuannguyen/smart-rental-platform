using SmartRentalPlatform.Contracts.Users;

namespace SmartRentalPlatform.Application.Users;

public interface IUserService
{
    Task<CurrentUserResponse> GetCurrentUserAsync(
        CancellationToken cancellationToken = default);

    Task<UserProfileResponse> GetUserProfileAsync(
        CancellationToken cancellationToken = default);

    Task<UserProfileResponse> UpdateUserProfileAsync(
        UpdateUserProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<LandlordEligibilityResponse> GetLandlordEligibilityAsync(
        CancellationToken cancellationToken = default);

    Task AssignDefaultTenantRoleAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task GrantLandlordRoleAfterRoomingHouseApprovedAsync(
        Guid roomingHouseId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserSessionResponse>> GetActiveSessionsAsync(
        CancellationToken cancellationToken = default);

    Task RevokeSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
}