using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Users;
using SmartRentalPlatform.Application.Roles;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Users;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Route("api/users")]
public class MeController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IRoleService _roleService;

    public MeController(IUserService userService, IRoleService roleService)
    {
        _userService = userService;
        _roleService = roleService;
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<CurrentUserResponse>>> GetMe(
        CancellationToken cancellationToken)
    {
        var result = await _userService.GetCurrentUserAsync(cancellationToken);

        return Ok(new ApiResponse<CurrentUserResponse>
        {
            Success = true,
            Message = "Lấy thông tin người dùng hiện tại thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("me/landlord-eligibility")]
    public async Task<ActionResult<ApiResponse<LandlordEligibilityResponse>>> GetLandlordEligibility(
        CancellationToken cancellationToken)
    {
        var result = await _userService.GetLandlordEligibilityAsync(cancellationToken);

        return Ok(new ApiResponse<LandlordEligibilityResponse>
        {
            Success = true,
            Message = "Kiểm tra điều kiện đăng ký chủ trọ thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpGet("me/role-status")]
    public async Task<ActionResult<ApiResponse<UserRoleStatusResponse>>> GetRoleStatus(
        CancellationToken cancellationToken)
    {
        var result = await _roleService.GetUserRoleStatusAsync(cancellationToken);

        return Ok(new ApiResponse<UserRoleStatusResponse>
        {
            Success = true,
            Message = "Lấy thông tin trạng thái vai trò thành công.",
            Data = result
        });
    }
}
