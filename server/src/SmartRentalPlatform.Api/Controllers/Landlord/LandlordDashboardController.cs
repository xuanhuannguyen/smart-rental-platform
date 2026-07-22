using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.LandlordDashboard;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.LandlordDashboard.Responses;

namespace SmartRentalPlatform.Api.Controllers.Landlord;

[ApiController]
[Authorize(Roles = "Landlord")]
[Route("api/landlord/dashboard")]
public sealed class LandlordDashboardController(
    ILandlordDashboardService dashboardService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<LandlordDashboardResponse>>> GetDashboard(
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken)
    {
        var result = await dashboardService.GetDashboardAsync(
            currentUserService.GetRequiredUserId("Không tìm thấy người dùng đang đăng nhập."),
            year,
            month,
            cancellationToken);

        return Ok(new ApiResponse<LandlordDashboardResponse>
        {
            Success = true,
            Message = "Tải dashboard chủ trọ thành công.",
            Data = result
        });
    }
}
