using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.RoomDeposits;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RentalRequests.Responses;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Route("api/room-deposits")]
public class RoomDepositsController : ControllerBase
{
    private readonly IRoomDepositService roomDepositService;
    private readonly ICurrentUserService currentUserService;

    public RoomDepositsController(
        IRoomDepositService roomDepositService,
        ICurrentUserService currentUserService)
    {
        this.roomDepositService = roomDepositService;
        this.currentUserService = currentUserService;
    }

    [Authorize]
    [HttpPost("{id:guid}/pay")]
    public async Task<ActionResult<ApiResponse<RoomDepositResponse>>> Pay(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await roomDepositService.PayAsync(userId, id, cancellationToken);

        if (result is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.RoomDepositNotFound,
                Message = "Không tìm thấy khoản cọc."
            });
        }

        return Ok(new ApiResponse<RoomDepositResponse>
        {
            Success = true,
            Message = "Thanh toán cọc thành công.",
            Data = result
        });
    }

    private Guid GetCurrentUserId()
    {
        return currentUserService.GetRequiredUserId("Không tìm thấy mã người dùng đã đăng nhập.");
    }
}
