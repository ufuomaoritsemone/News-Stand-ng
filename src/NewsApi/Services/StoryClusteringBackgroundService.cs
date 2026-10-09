using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NewsApi.Services;

public class StoryClusteringBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _config;
    private readonly ILogger<StoryClusteringBackgroundService> _logger;

    public StoryClusteringBackgroundService(
        IServiceProvider serviceProvider,
        IConfiguration config,
        ILogger<StoryClusteringBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StoryClusteringBackgroundService started.");

        // Wait a few seconds on startup to let DB initialize and migrations complete
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var clusteringService = scope.ServiceProvider.GetRequiredService<IStoryClusteringService>();

                var clusterCount = await clusteringService.RunClusteringAsync(stoppingToken);
                _logger.LogInformation("StoryClusteringBackgroundService completed run. Active clusters: {Count}", clusterCount);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error occurred during story clustering background execution");
            }

            var intervalMinutes = _config.GetValue<int>("Clustering:IntervalMinutes", 15);
            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }
    }
}
