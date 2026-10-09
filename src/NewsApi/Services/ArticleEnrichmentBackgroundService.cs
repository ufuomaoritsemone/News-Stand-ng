namespace NewsApi.Services;

/// <summary>
/// Background worker that periodically scans for unenriched articles and runs AI/ML enrichment
/// (Key Takeaways, Timeline Facts, FAQ Schema).
/// </summary>
public class ArticleEnrichmentBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ArticleEnrichmentBackgroundService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<ArticleEnrichmentBackgroundService> _logger = logger;

    // Run every 10 minutes in the background
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ArticleEnrichmentBackgroundService started. Periodic interval: {Interval}", Interval);

        // Initial delay to avoid database contention during startup
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var enrichmentService = scope.ServiceProvider.GetRequiredService<IArticleEnrichmentService>();

                _logger.LogInformation("ArticleEnrichmentBackgroundService: Checking for unenriched articles...");
                int processed = await enrichmentService.ProcessUnenrichedBatchAsync(batchSize: 20, stoppingToken);

                if (processed > 0)
                {
                    _logger.LogInformation("ArticleEnrichmentBackgroundService: Successfully enriched {Count} articles", processed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error occurred in ArticleEnrichmentBackgroundService cycle");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("ArticleEnrichmentBackgroundService stopping.");
    }
}
