using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartRentalPlatform.Application.RoomingHouses.ReviewModeration;

namespace SmartRentalPlatform.Infrastructure.BackgroundServices;

public sealed class ReviewAiModerationWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<ReviewAiModerationWorker> logger;

    public ReviewAiModerationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ReviewAiModerationWorker> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var moderationService = scope.ServiceProvider.GetRequiredService<IReviewAiModerationService>();
                await moderationService.ModeratePendingReviewsAsync(10, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                try
                {
                    logger.LogError(ex, "Could not moderate pending rooming house reviews.");
                }
                catch
                {
                    // Do not let logging provider failures terminate the worker.
                }
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
