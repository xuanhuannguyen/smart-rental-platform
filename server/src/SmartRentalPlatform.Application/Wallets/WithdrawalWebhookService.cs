using Microsoft.Extensions.Logging;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Domain.Entities.Payments;
using SmartRentalPlatform.Domain.Enums.Payments;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Payments;

namespace SmartRentalPlatform.Application.Wallets;

public class WithdrawalWebhookService : IWithdrawalWebhookService
{
    private readonly IAppDbContext context;
    private readonly IWalletService walletService;
    private readonly IPaymentRowLockService rowLockService;
    private readonly IPayOSWebhookSignatureVerifier payOSSignatureVerifier;
    private readonly ILogger<WithdrawalWebhookService> logger;

    public WithdrawalWebhookService(
        IAppDbContext context,
        IWalletService walletService,
        IPaymentRowLockService rowLockService,
        IPayOSWebhookSignatureVerifier payOSSignatureVerifier,
        ILogger<WithdrawalWebhookService> logger)
    {
        this.context = context;
        this.walletService = walletService;
        this.rowLockService = rowLockService;
        this.payOSSignatureVerifier = payOSSignatureVerifier;
        this.logger = logger;
    }

    public async Task ProcessWebhookAsync(
        string providerOrderCode,
        string status,
        string payload,
        string? signature,
        bool skipSignatureVerification = false,
        CancellationToken cancellationToken = default)
    {
        if (!skipSignatureVerification && !payOSSignatureVerifier.VerifyPayout(payload, signature))
        {
            logger.LogWarning("Invalid webhook signature for payout {ProviderOrderCode}", providerOrderCode);
            return;
        }
        var log = new WithdrawalWebhookLog
        {
            ProviderOrderCode = providerOrderCode,
            Status = status,
            Payload = payload
        };
        
        await using var transaction = await context.BeginTransactionAsync(cancellationToken);
        
        try
        {
            var withdrawalRequest = await rowLockService.LockWithdrawalRequestByProviderOrderCodeAsync(providerOrderCode, cancellationToken);
            
            if (withdrawalRequest == null)
            {
                logger.LogWarning("Received webhook for unknown withdrawal {ProviderOrderCode}", providerOrderCode);
                return;
            }

            log.WithdrawalRequestId = withdrawalRequest.Id;
            var logExists = await context.WithdrawalWebhookLogs.AnyAsync(x => 
                x.WithdrawalRequestId == log.WithdrawalRequestId && x.Status == status, cancellationToken);
            if (!logExists)
            {
                context.WithdrawalWebhookLogs.Add(log);
            }

            if (withdrawalRequest.Status != WithdrawalStatus.Processing)
            {
                logger.LogInformation("Withdrawal {ProviderOrderCode} is already processed with status {Status}", providerOrderCode, withdrawalRequest.Status);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            var totalDeduction = withdrawalRequest.Amount + withdrawalRequest.Fee;

            if (status == "SUCCESS" || status == "SUCCEEDED" || status == "COMPLETED")
            {
                withdrawalRequest.Status = WithdrawalStatus.Succeeded;
                await walletService.DebitFromReservedAsync(
                    withdrawalRequest.WalletAccountId,
                    totalDeduction,
                    WalletTransactionType.WalletWithdrawalSucceeded,
                    new WalletTransactionMetadata
                    {
                        RelatedEntityType = "WithdrawalRequest",
                        RelatedEntityId = withdrawalRequest.Id,
                        Description = $"Successful withdrawal {withdrawalRequest.ProviderOrderCode}"
                    },
                    cancellationToken);
            }
            else if (status == "FAILED" || status == "REJECTED" || status == "CANCELLED")
            {
                withdrawalRequest.Status = WithdrawalStatus.Failed;
                withdrawalRequest.Description = $"Payout failed with status: {status}";
                
                await walletService.DecreaseReservedAsync(
                    withdrawalRequest.WalletAccountId,
                    totalDeduction,
                    WalletTransactionType.WalletWithdrawalRefund,
                    new WalletTransactionMetadata
                    {
                        RelatedEntityType = "WithdrawalRequest",
                        RelatedEntityId = withdrawalRequest.Id,
                        Description = $"Refund failed withdrawal {withdrawalRequest.ProviderOrderCode}"
                    },
                    cancellationToken);
            }
            else
            {
                logger.LogWarning("Received unhandled webhook status {Status} for withdrawal {ProviderOrderCode}", status, providerOrderCode);
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process webhook for withdrawal {ProviderOrderCode}", providerOrderCode);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
