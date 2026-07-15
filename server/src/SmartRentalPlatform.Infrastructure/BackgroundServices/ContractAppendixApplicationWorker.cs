using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartRentalPlatform.Application.RentalContracts;

namespace SmartRentalPlatform.Infrastructure.BackgroundServices;

public class ContractAppendixApplicationWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly ILogger<ContractAppendixApplicationWorker> logger;

    public ContractAppendixApplicationWorker(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ContractAppendixApplicationWorker> logger)
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
                var contractAppendixService = scope.ServiceProvider.GetRequiredService<IContractAppendixService>();

                var appliedCount = await contractAppendixService.ApplyDueAppendicesAsync(stoppingToken);
                if (appliedCount > 0)
                {
                    logger.SafeLogInformation("Applied {AppliedCount} due contract appendices.", appliedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.SafeLogError(ex, "Could not apply due contract appendices.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
