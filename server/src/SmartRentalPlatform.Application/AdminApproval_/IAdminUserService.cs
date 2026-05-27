using System.Threading;
using System.Threading.Tasks;
using SmartRentalPlatform.Contracts.Admin;

namespace SmartRentalPlatform.Application.AdminApproval.Services;

public interface IAdminUserService
{
    Task<AdminUserListResponse> GetUsersAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AdminUserDetailResponse?> GetUserDetailAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
