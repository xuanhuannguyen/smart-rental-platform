using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Contracts.Admin;
using SmartRentalPlatform.Application.AdminApproval;
using SmartRentalPlatform.Contracts.Common;

namespace SmartRentalPlatform.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _adminUserService;

    public AdminUsersController(IAdminUserService adminUserService)
    {
        _adminUserService = adminUserService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<AdminUserListResponse>>> GetUsers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1 || pageSize is < 1 or > 100)
        {
            return BadRequest(ValidationError("Thông tin phân trang không hợp lệ."));
        }

        var result = await _adminUserService.GetUsersAsync(pageNumber, pageSize, cancellationToken);
        return Ok(new ApiResponse<AdminUserListResponse>
        {
            Success = true,
            Message = "Tải danh sách người dùng thành công.",
            Data = result
        });
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<ApiResponse<AdminUserDetailResponse>>> GetUserDetail(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminUserService.GetUserDetailAsync(userId, cancellationToken);
        if (result is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "Không tìm thấy người dùng."
            });
        }

        return Ok(new ApiResponse<AdminUserDetailResponse>
        {
            Success = true,
            Message = "Tải chi tiết người dùng thành công.",
            Data = result
        });
    }

    private static ApiErrorResponse ValidationError(string message)
    {
        return new ApiErrorResponse
        {
            Success = false,
            ErrorCode = ErrorCodes.ValidationError,
            Message = message
        };
    }
}
