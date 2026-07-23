using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Payments;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Wallets.Requests;
using SmartRentalPlatform.Contracts.Wallets.Responses;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Authorize(Roles = "Tenant,Landlord")]
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

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<WalletTopUpHistoryResponse>>>> GetTopUpHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await payOSTopUpService.GetTopUpHistoryAsync(userId, page, pageSize, cancellationToken);

        return Ok(new ApiResponse<PagedResult<WalletTopUpHistoryResponse>>
        {
            Success = true,
            Message = "Lấy lịch sử yêu cầu nạp ví thành công.",
            Data = result
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<WalletTopUpHistoryResponse>>> GetTopUp(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await payOSTopUpService.GetTopUpAsync(userId, id, cancellationToken);

        return Ok(new ApiResponse<WalletTopUpHistoryResponse>
        {
            Success = true,
            Message = "Lấy thông tin giao dịch nạp ví thành công.",
            Data = result
        });
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
        return currentUserService.GetRequiredUserIdForAction();
    }
}
