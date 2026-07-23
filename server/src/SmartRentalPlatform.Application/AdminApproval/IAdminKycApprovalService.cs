using SmartRentalPlatform.Contracts.Admin;

namespace SmartRentalPlatform.Application.AdminApproval;

public interface IAdminKycApprovalService
{
    Task<AdminKycListResponse> GetPendingAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AdminKycDetailResponse?> GetDetailAsync(
        Guid kycId,
        CancellationToken cancellationToken = default);

    Task<bool> ApproveAsync(
        Guid kycId,
        Guid adminId,
        AdminApproveKycRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> RejectAsync(
        Guid kycId,
        Guid adminId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<System.Collections.Generic.List<AdminKycDetailResponse>> GetHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
