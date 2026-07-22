using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartRentalPlatform.Application.RentalContracts;

namespace SmartRentalPlatform.Infrastructure.BackgroundServices;

public class RentalContractMoveInActivationWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly ILogger<RentalContractMoveInActivationWorker> logger;

    public RentalContractMoveInActivationWorker(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<RentalContractMoveInActivationWorker> logger)
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

                var activatedCount = await rentalContractService.ActivatePendingMoveInsAsync(stoppingToken);
                if (activatedCount > 0)
                {
                    logger.SafeLogInformation("Activated {ActivatedCount} rental contracts with pending move-ins.", activatedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.SafeLogError(ex, "Could not activate pending move-ins for rental contracts.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
