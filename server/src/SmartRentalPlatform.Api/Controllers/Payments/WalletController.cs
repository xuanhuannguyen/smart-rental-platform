using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Wallets;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Wallets.Responses;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me/wallet")]
public class WalletController : ControllerBase
{
    private readonly IWalletService walletService;
    private readonly ICurrentUserService currentUserService;

    public WalletController(
        IWalletService walletService,
        ICurrentUserService currentUserService)
    {
        this.walletService = walletService;
        this.currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<WalletResponse>>> GetMyWallet(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await walletService.GetMyWalletAsync(userId, cancellationToken);

        return Ok(new ApiResponse<WalletResponse>
        {
            Success = true,
            Message = "Lay thong tin vi thanh cong.",
            Data = result
        });
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<ApiResponse<PagedResult<WalletTransactionResponse>>>> GetTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await walletService.GetTransactionsAsync(userId, page, pageSize, cancellationToken);

        return Ok(new ApiResponse<PagedResult<WalletTransactionResponse>>
        {
            Success = true,
            Message = "Lay lich su giao dich vi thanh cong.",
            Data = result
        });
    }

    private Guid GetCurrentUserId()
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            throw new UnauthorizedException(
                ErrorCodes.Unauthorized,
                "Ban can dang nhap de thuc hien thao tac nay.");
        }

        return currentUserService.UserId.Value;
    }
}
