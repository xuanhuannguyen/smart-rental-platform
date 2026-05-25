using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Profiles;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Profiles;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Route("api/users")]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    [Authorize]
    [HttpGet("me/profile")]
    public async Task<ActionResult<ApiResponse<UserProfileResponse>>> GetProfile(
        CancellationToken cancellationToken)
    {
        var result = await _profileService.GetUserProfileAsync(cancellationToken);

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
        var result = await _profileService.UpdateUserProfileAsync(request, cancellationToken);

        return Ok(new ApiResponse<UserProfileResponse>
        {
            Success = true,
            Message = "Cập nhật hồ sơ thành công.",
            Data = result
        });
    }
}
