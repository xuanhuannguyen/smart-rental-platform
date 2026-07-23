using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartRentalPlatform.Application.RoomingHouses;
using SmartRentalPlatform.Contracts.RoomingHouses.Requests;

namespace SmartRentalPlatform.Infrastructure.BackgroundServices;

public sealed class PublicSearchCacheWarmupWorker : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<PublicSearchCacheWarmupWorker> logger;

    public PublicSearchCacheWarmupWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PublicSearchCacheWarmupWorker> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);

            await using var scope = scopeFactory.CreateAsyncScope();
            var roomingHouseQueryService = scope.ServiceProvider.GetRequiredService<IRoomingHouseQueryService>();

            await roomingHouseQueryService.SearchPublicAsync(
                new RoomingHouseSearchRequest
                {
                    Page = 1,
                    PageSize = 12,
                    Sort = "relevance"
                },
                stoppingToken);

            logger.LogInformation("Public rooming house search cache warmed successfully.");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Application is shutting down.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Public rooming house search cache warmup failed.");
        }
    }
}
