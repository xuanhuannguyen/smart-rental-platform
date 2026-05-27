using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Users;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Users;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
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
    [HttpGet("me/profile")]
    public async Task<ActionResult<ApiResponse<UserProfileResponse>>> GetProfile(
        CancellationToken cancellationToken)
    {
        var result = await _userService.GetUserProfileAsync(cancellationToken);

        return Ok(new ApiResponse<UserProfileResponse>
        {
            Success = true,
            Message = "Lấy thông tin hồ sơ thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpPut("me/profile")]
    public async Task<ActionResult<ApiResponse<UserProfileResponse>>> UpdateProfile(
        [FromBody] UpdateUserProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _userService.UpdateUserProfileAsync(request, cancellationToken);

        return Ok(new ApiResponse<UserProfileResponse>
        {
            Success = true,
            Message = "Cập nhật hồ sơ thành công.",
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
    [HttpGet("me/sessions")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<UserSessionResponse>>>> GetActiveSessions(
        CancellationToken cancellationToken)
    {
        var result = await _userService.GetActiveSessionsAsync(cancellationToken);

        return Ok(new ApiResponse<IReadOnlyCollection<UserSessionResponse>>
        {
            Success = true,
            Message = "Lấy danh sách thiết bị đã đăng nhập thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpDelete("me/sessions/{id}")]
    public async Task<ActionResult<ApiResponse<object>>> RevokeSession(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _userService.RevokeSessionAsync(id, cancellationToken);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Đăng xuất thiết bị thành công."
        });
    }
}
