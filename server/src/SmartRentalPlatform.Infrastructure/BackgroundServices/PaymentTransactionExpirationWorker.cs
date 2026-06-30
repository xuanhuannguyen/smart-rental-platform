using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartRentalPlatform.Application.Payments;

namespace SmartRentalPlatform.Infrastructure.BackgroundServices;

public class PaymentTransactionExpirationWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly ILogger<PaymentTransactionExpirationWorker> logger;

    public PaymentTransactionExpirationWorker(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<PaymentTransactionExpirationWorker> logger)
    {
        this.serviceScopeFactory = serviceScopeFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var topUpService = scope.ServiceProvider.GetRequiredService<IPayOSTopUpService>();

                var expiredCount = await topUpService.ExpireOverduePendingTopUpsAsync(stoppingToken);
                if (expiredCount > 0)
                {
                    logger.LogInformation("Expired {ExpiredCount} overdue pending wallet top-up transactions.", expiredCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not expire overdue pending wallet top-up transactions.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
