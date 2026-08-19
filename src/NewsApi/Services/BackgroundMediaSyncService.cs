using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NewsApi.Services;

/// <summary>
/// Background hosted service that periodically synchronizes YouTube channels, YouTube trending stories,
/// and live breaking social feeds at regular intervals.
/// </summary>
public class BackgroundMediaSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundMediaSyncService> _logger;

    public BackgroundMediaSyncService(IServiceProvider serviceProvider, ILogger<BackgroundMediaSyncService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundMediaSyncService started. Initial media synchronization scheduled in 5 seconds...");

        // Initial grace delay to allow database schema verification and initial seed to complete
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting scheduled background media sync (YouTube channels, Trending videos, and Live Social wires)...");

                using var scope = _serviceProvider.CreateScope();
                var ytService = scope.ServiceProvider.GetRequiredService<YouTubeFeedService>();

                // 1. Sync YouTube channel video stories
                try
                {
                    var newYtCount = await ytService.SyncAllChannelsAsync(stoppingToken);
                    _logger.LogInformation("YouTube channel sync completed: {Count} new video stories.", newYtCount);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Error syncing YouTube channel feeds during background cycle.");
                }

                // 2. Sync YouTube trending news stories
                try
                {
                    var trendingCount = await ytService.SyncTrendingNewsAsync(stoppingToken);
                    _logger.LogInformation("YouTube trending sync completed: {Count} stories updated.", trendingCount);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Error syncing YouTube trending stories during background cycle.");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unexpected error in BackgroundMediaSyncService cycle.");
            }

            // Repeat every 15 minutes
            _logger.LogInformation("Background media sync cycle finished. Next sync scheduled in 15 minutes.");
            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
}
