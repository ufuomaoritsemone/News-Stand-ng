using NewsApi.Models;

namespace NewsApi.Services;

/// <summary>
/// Service interface for Google Search Console telemetry, search performance analytics,
/// and automated crawl/indexation diagnostics.
/// </summary>
public interface ISearchConsoleService
{
    /// <summary>
    /// Retrieves aggregated search performance metrics (clicks, impressions, CTR, position)
    /// across requested time window and dimensions.
    /// </summary>
    Task<SearchPerformanceSummaryDto> GetSearchAnalyticsAsync(
        int days = 28,
        string dimension = "query",
        int limit = 50,
        CancellationToken ct = default);

    /// <summary>
    /// Computes full crawlability and indexation diagnostics across site entities,
    /// sitemaps, and IndexNow status.
    /// </summary>
    Task<CrawlDiagnosticsDto> GetCrawlDiagnosticsAsync(CancellationToken ct = default);

    /// <summary>
    /// Performs deep URL inspection evaluating indexability, canonical tags,
    /// mobile usability, and schema.org rich results.
    /// </summary>
    Task<UrlInspectionResultDto> InspectUrlAsync(string url, CancellationToken ct = default);

    /// <summary>
    /// Evaluates live health, status, and response latency for all registered XML sitemaps.
    /// </summary>
    Task<List<SitemapDiagnosticDto>> GetSitemapHealthAsync(CancellationToken ct = default);
}
