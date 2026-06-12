using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartRentalPlatform.Application.RoomDeposits;

namespace SmartRentalPlatform.Infrastructure.BackgroundServices;

public class RoomDepositExpirationWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly ILogger<RoomDepositExpirationWorker> logger;

    public RoomDepositExpirationWorker(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<RoomDepositExpirationWorker> logger)
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
                var roomDepositService = scope.ServiceProvider.GetRequiredService<IRoomDepositService>();

                var expiredCount = await roomDepositService.ExpireOverdueAsync(stoppingToken);
                if (expiredCount > 0)
                {
                    logger.LogInformation("Đã xử lý {ExpiredCount} khoản cọc quá hạn.", expiredCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Không thể xử lý các khoản cọc quá hạn.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
