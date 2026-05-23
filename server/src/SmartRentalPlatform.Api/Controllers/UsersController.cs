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
}
