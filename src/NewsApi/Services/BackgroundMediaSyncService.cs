using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NewsApi.Services;

/// <summary>
/// Background hosted service that periodically synchronizes YouTube channels,
/// YouTube trending stories, and live breaking social feeds.
/// Fix #20: resolves IYouTubeFeedService (interface, not concrete class).
/// Fix #30: poll interval is configurable via MediaSync:IntervalMinutes.
/// </summary>
public class BackgroundMediaSyncService(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<BackgroundMediaSyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = configuration.GetValue("MediaSync:IntervalMinutes", 15);
        logger.LogInformation(
            "BackgroundMediaSyncService started. Sync interval: {IntervalMinutes} minutes.",
            intervalMinutes);

        // Initial grace delay — allow DB schema initialization to complete first
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunSyncCycleAsync(stoppingToken);

            logger.LogInformation(
                "Background media sync cycle finished. Next sync in {Minutes} minutes.",
                intervalMinutes);

            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }
    }

    private async Task RunSyncCycleAsync(CancellationToken ct)
    {
        logger.LogInformation("Starting scheduled background media sync...");

        using var scope     = serviceProvider.CreateScope();
        var ytService       = scope.ServiceProvider.GetRequiredService<IYouTubeFeedService>(); // Fix #20

        // 1. Sync YouTube channel video stories
        try
        {
            var count = await ytService.SyncAllChannelsAsync(ct);
            logger.LogInformation("YouTube channel sync completed: {Count} new stories.", count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Error syncing YouTube channel feeds.");
        }

        // 2. Sync YouTube trending news stories
        try
        {
            var count = await ytService.SyncTrendingNewsAsync(ct);
            logger.LogInformation("YouTube trending sync completed: {Count} stories updated.", count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Error syncing YouTube trending stories.");
        }
    }
}
