using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartRentalPlatform.Application.RentalContracts;

namespace SmartRentalPlatform.Infrastructure.BackgroundServices;

public class RentalContractExpirationWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly ILogger<RentalContractExpirationWorker> logger;

    public RentalContractExpirationWorker(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<RentalContractExpirationWorker> logger)
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
                var rentalContractService = scope.ServiceProvider.GetRequiredService<IRentalContractService>();

                var expiredCount = await rentalContractService.ExpireOverdueTenantSignaturesAsync(stoppingToken);
                if (expiredCount > 0)
                {
                    logger.LogInformation("Đã xử lý {ExpiredCount} hợp đồng quá hạn ký.", expiredCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Không thể xử lý các hợp đồng quá hạn ký.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
