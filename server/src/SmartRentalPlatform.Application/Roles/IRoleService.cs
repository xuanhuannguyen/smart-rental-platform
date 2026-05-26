using SmartRentalPlatform.Contracts.Users;

namespace SmartRentalPlatform.Application.Roles;

public interface IRoleService
{
    Task<UserRoleStatusResponse> GetUserRoleStatusAsync(
        CancellationToken cancellationToken = default);

    Task AssignDefaultTenantRoleAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task GrantLandlordRoleAfterRoomingHouseApprovedAsync(
        Guid roomingHouseId,
        CancellationToken cancellationToken = default);
}
