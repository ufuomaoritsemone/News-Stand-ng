using System.Diagnostics;

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

                _logger.LogInformation(
                    "\n╔══════════════════════════════════════════════════════════════════════════════╗\n" +
                    "║ 🚀 [ENRICHMENT BATCH ENGAGED] Scanning for Unenriched Articles               ║\n" +
                    "║ Engine: Gemini Generative AI (Local TextRank ML Automatic Fallback)          ║\n" +
                    "╚══════════════════════════════════════════════════════════════════════════════╝");

                var sw = Stopwatch.StartNew();
                int processed = await enrichmentService.ProcessUnenrichedBatchAsync(batchSize: 20, stoppingToken);
                sw.Stop();

                if (processed > 0)
                {
                    _logger.LogInformation(
                        "\n╔══════════════════════════════════════════════════════════════════════════════╗\n" +
                        "║ 🏁 [ENRICHMENT BATCH COMPLETED] Successfully enriched {Count} articles ({ElapsedMs} ms) ║\n" +
                        "╚══════════════════════════════════════════════════════════════════════════════╝",
                        processed, sw.ElapsedMilliseconds);
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
