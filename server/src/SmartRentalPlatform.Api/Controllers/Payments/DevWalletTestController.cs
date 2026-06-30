using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Wallets;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Wallets.Responses;
using SmartRentalPlatform.Domain.Enums.Payments;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Tags("DEV ONLY - TEMPORARY TEST ENDPOINT")]
[Route("api/dev/wallet-test")]
public class DevWalletTestController : ControllerBase
{
    private const string RelatedEntityType = "DevWalletTest";

    private readonly IWalletService walletService;
    private readonly ICurrentUserService currentUserService;
    private readonly IWebHostEnvironment environment;

    public DevWalletTestController(
        IWalletService walletService,
        ICurrentUserService currentUserService,
        IWebHostEnvironment environment)
    {
        this.walletService = walletService;
        this.currentUserService = currentUserService;
        this.environment = environment;
    }

    /// <summary>DEV ONLY - TEMPORARY TEST ENDPOINT: debit current user's wallet.</summary>
    [HttpPost("debit")]
    [EndpointSummary("DEV ONLY - TEMPORARY TEST ENDPOINT: debit current user's wallet.")]
    public async Task<ActionResult<ApiResponse<DevWalletMutationResponse>>> Debit(
        DevWalletAmountRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled())
        {
            return NotFound();
        }

        var wallet = await walletService.GetOrCreateWalletAsync(GetCurrentUserId(), cancellationToken);
        var result = await walletService.DebitAsync(
            wallet.Id,
            request.Amount,
            WalletTransactionType.InvoicePayment,
            CreateMetadata(request.Note),
            cancellationToken);

        return Ok(CreateMutationApiResponse(result, "DEV debit wallet test completed."));
    }

    /// <summary>DEV ONLY - TEMPORARY TEST ENDPOINT: credit current user's wallet.</summary>
    [HttpPost("credit")]
    [EndpointSummary("DEV ONLY - TEMPORARY TEST ENDPOINT: credit current user's wallet.")]
    public async Task<ActionResult<ApiResponse<DevWalletMutationResponse>>> Credit(
        DevWalletAmountRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled())
        {
            return NotFound();
        }

        var wallet = await walletService.GetOrCreateWalletAsync(GetCurrentUserId(), cancellationToken);
        var result = await walletService.CreditAsync(
            wallet.Id,
            request.Amount,
            WalletTransactionType.ManualAdjustment,
            CreateMetadata(request.Note),
            cancellationToken);

        return Ok(CreateMutationApiResponse(result, "DEV credit wallet test completed."));
    }

    /// <summary>DEV ONLY - TEMPORARY TEST ENDPOINT: transfer between two wallets atomically.</summary>
    [HttpPost("transfer")]
    [EndpointSummary("DEV ONLY - TEMPORARY TEST ENDPOINT: transfer between two wallets atomically.")]
    public async Task<ActionResult<ApiResponse<DevWalletTransferResult>>> Transfer(
        DevWalletTransferRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled())
        {
            return NotFound();
        }

        var sourceWallet = await walletService.GetOrCreateWalletAsync(GetCurrentUserId(), cancellationToken);
        var targetWallet = await walletService.GetOrCreateWalletAsync(request.TargetUserId, cancellationToken);
        var transferGroupId = Guid.NewGuid();

        var result = await walletService.TransferAsync(
            sourceWallet.Id,
            targetWallet.Id,
            request.Amount,
            WalletTransactionType.InvoicePayment,
            WalletTransactionType.InvoiceReceive,
            CreateMetadata(request.Note, transferGroupId),
            cancellationToken);

        return Ok(new ApiResponse<DevWalletTransferResult>
        {
            Success = true,
            Message = "DEV wallet transfer test completed.",
            Data = new DevWalletTransferResult
            {
                TransactionId = result.TransferGroupId,
                TransferGroupId = result.TransferGroupId,
                PaymentMethod = PaymentMethod.InternalWallet.ToString(),
                PaymentPurpose = PaymentPurpose.DevTest.ToString(),
                DebitWalletTransactionId = result.DebitTransaction.Id,
                CreditWalletTransactionId = result.CreditTransaction.Id,
                SourceBalanceBefore = result.DebitTransaction.BalanceBefore,
                SourceBalanceAfter = result.DebitTransaction.BalanceAfter,
                TargetBalanceBefore = result.CreditTransaction.BalanceBefore,
                TargetBalanceAfter = result.CreditTransaction.BalanceAfter,
                CreatedAt = result.DebitTransaction.CreatedAt
            }
        });
    }

    /// <summary>DEV ONLY - TEMPORARY TEST ENDPOINT: increase current user's reserved balance.</summary>
    [HttpPost("reserve")]
    [EndpointSummary("DEV ONLY - TEMPORARY TEST ENDPOINT: increase current user's reserved balance.")]
    public async Task<ActionResult<ApiResponse<DevWalletMutationResponse>>> Reserve(
        DevWalletAmountRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled())
        {
            return NotFound();
        }

        var wallet = await walletService.GetOrCreateWalletAsync(GetCurrentUserId(), cancellationToken);
        var result = await walletService.IncreaseReservedAsync(
            wallet.Id,
            request.Amount,
            WalletTransactionType.ManualAdjustment,
            CreateMetadata(request.Note ?? "dev reserve test"),
            cancellationToken);

        return Ok(CreateMutationApiResponse(result, "DEV reserve wallet test completed."));
    }

    /// <summary>DEV ONLY - TEMPORARY TEST ENDPOINT: decrease current user's reserved balance.</summary>
    [HttpPost("release-reserve")]
    [EndpointSummary("DEV ONLY - TEMPORARY TEST ENDPOINT: decrease current user's reserved balance.")]
    public async Task<ActionResult<ApiResponse<DevWalletMutationResponse>>> ReleaseReserve(
        DevWalletAmountRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled())
        {
            return NotFound();
        }

        var wallet = await walletService.GetOrCreateWalletAsync(GetCurrentUserId(), cancellationToken);
        var result = await walletService.DecreaseReservedAsync(
            wallet.Id,
            request.Amount,
            WalletTransactionType.ManualAdjustment,
            CreateMetadata(request.Note ?? "dev release reserve test"),
            cancellationToken);

        return Ok(CreateMutationApiResponse(result, "DEV release reserve wallet test completed."));
    }

    private bool IsEnabled()
    {
        return environment.IsDevelopment();
    }

    private Guid GetCurrentUserId()
    {
        return currentUserService.GetRequiredUserIdForAction();
    }

    private static WalletTransactionMetadata CreateMetadata(string? note, Guid? transferGroupId = null)
    {
        return new WalletTransactionMetadata
        {
            TransferGroupId = transferGroupId,
            RelatedEntityType = RelatedEntityType,
            Description = string.IsNullOrWhiteSpace(note) ? "DEV wallet test." : note.Trim()
        };
    }

    private static ApiResponse<DevWalletMutationResponse> CreateMutationApiResponse(
        WalletMutationResponse result,
        string message)
    {
        return new ApiResponse<DevWalletMutationResponse>
        {
            Success = true,
            Message = message,
            Data = new DevWalletMutationResponse
            {
                TransactionId = result.Transaction.Id,
                WalletTransactionId = result.Transaction.Id,
                PaymentMethod = PaymentMethod.InternalWallet.ToString(),
                PaymentPurpose = PaymentPurpose.DevTest.ToString(),
                TransactionType = result.Transaction.TransactionType,
                Direction = result.Transaction.Direction,
                BalanceBefore = result.Transaction.BalanceBefore,
                BalanceAfter = result.Transaction.BalanceAfter,
                ReservedBalanceBefore = result.Transaction.ReservedBalanceBefore,
                ReservedBalanceAfter = result.Transaction.ReservedBalanceAfter,
                CreatedAt = result.Transaction.CreatedAt
            }
        };
    }

    public class DevWalletAmountRequest
    {
        public decimal Amount { get; set; }
        public string? Note { get; set; }
    }

    public sealed class DevWalletTransferRequest : DevWalletAmountRequest
    {
        public Guid TargetUserId { get; set; }
    }

    public sealed class DevWalletMutationResponse
    {
        public Guid TransactionId { get; set; }
        public Guid WalletTransactionId { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentPurpose { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public decimal BalanceBefore { get; set; }
        public decimal BalanceAfter { get; set; }
        public decimal ReservedBalanceBefore { get; set; }
        public decimal ReservedBalanceAfter { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public sealed class DevWalletTransferResult
    {
        public Guid TransactionId { get; set; }
        public Guid TransferGroupId { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentPurpose { get; set; } = string.Empty;
        public Guid DebitWalletTransactionId { get; set; }
        public Guid CreditWalletTransactionId { get; set; }
        public decimal SourceBalanceBefore { get; set; }
        public decimal SourceBalanceAfter { get; set; }
        public decimal TargetBalanceBefore { get; set; }
        public decimal TargetBalanceAfter { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
