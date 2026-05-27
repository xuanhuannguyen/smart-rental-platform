using SmartRentalPlatform.Contracts.Admin;

namespace SmartRentalPlatform.Application.AdminApproval.Services;

public interface IAdminRoomingHouseApprovalService
{
    Task<AdminRoomingHouseListResponse> GetPendingAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AdminRoomingHouseDetailResponse?> GetDetailAsync(
        Guid roomingHouseId,
        CancellationToken cancellationToken = default);

    Task<bool> ApproveAsync(
        Guid roomingHouseId,
        Guid adminId,
        CancellationToken cancellationToken = default);

    Task<bool> RejectAsync(
        Guid roomingHouseId,
        Guid adminId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<AdminRoomingHouseListResponse> GetPublicAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
