using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Payments;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Wallets.Requests;
using SmartRentalPlatform.Contracts.Wallets.Responses;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me/wallet/topups")]
public class WalletTopUpsController : ControllerBase
{
    private readonly IPayOSTopUpService payOSTopUpService;
    private readonly ICurrentUserService currentUserService;

    public WalletTopUpsController(
        IPayOSTopUpService payOSTopUpService,
        ICurrentUserService currentUserService)
    {
        this.payOSTopUpService = payOSTopUpService;
        this.currentUserService = currentUserService;
    }

    [HttpPost("payos")]
    public async Task<ActionResult<ApiResponse<CreatePayOSTopUpResponse>>> CreatePayOSTopUp(
        CreatePayOSTopUpRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await payOSTopUpService.CreateTopUpAsync(userId, request, cancellationToken);

        return Ok(new ApiResponse<CreatePayOSTopUpResponse>
        {
            Success = true,
            Message = "Tạo giao dịch nạp ví PayOS thành công.",
            Data = result
        });
    }

    private Guid GetCurrentUserId()
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            throw new UnauthorizedException(
                ErrorCodes.Unauthorized,
                "Bạn cần đăng nhập để thực hiện thao tác này.");
        }

        return currentUserService.UserId.Value;
    }
}
