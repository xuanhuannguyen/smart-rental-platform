using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Wallets;
using SmartRentalPlatform.Domain.Enums.Payments;
using System.Text.Json;

namespace SmartRentalPlatform.Infrastructure.BackgroundServices;

public class WithdrawalStatusSyncWorker : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<WithdrawalStatusSyncWorker> logger;

    public WithdrawalStatusSyncWorker(
        IServiceProvider serviceProvider,
        ILogger<WithdrawalStatusSyncWorker> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.SafeLogInformation("WithdrawalStatusSyncWorker is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncWithdrawalStatusesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.SafeLogError(ex, "Error occurred while syncing withdrawal statuses.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        logger.SafeLogInformation("WithdrawalStatusSyncWorker is stopping.");
    }

    private async Task SyncWithdrawalStatusesAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var payosClient = scope.ServiceProvider.GetRequiredService<IPayOSClient>();
        var webhookService = scope.ServiceProvider.GetRequiredService<IWithdrawalWebhookService>();

        var cutoffTime = DateTimeOffset.UtcNow.AddMinutes(-5);

        var pendingWithdrawals = await context.WithdrawalRequests
            .Where(x => x.Status == WithdrawalStatus.Processing && x.CreatedAt <= cutoffTime)
            .ToListAsync(cancellationToken);

        foreach (var req in pendingWithdrawals)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.ProviderTransactionCode))
                {
                    logger.SafeLogWarning("Withdrawal {ProviderOrderCode} is in Processing state but missing ProviderTransactionCode. Cannot sync status.", req.ProviderOrderCode);
                    continue;
                }

                var details = await payosClient.GetPayoutDetailsAsync(req.ProviderTransactionCode, cancellationToken);
                var actualState = details?.TransactionState ?? details?.ApprovalState;
                
                if (details != null && !string.IsNullOrWhiteSpace(actualState))
                {
                    await webhookService.ProcessWebhookAsync(
                        req.ProviderOrderCode,
                        actualState,
                        JsonSerializer.Serialize(details),
                        null,
                        true,
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.SafeLogError(ex, "Failed to sync status for withdrawal {ProviderOrderCode}", req.ProviderOrderCode);
            }
        }
    }
}
