using System.Text.Json.Serialization;

namespace NewsApi.Models;

/// <summary>
/// Query request parameters for Search Console performance analytics.
/// </summary>
public class SearchAnalyticsQueryDto
{
    public int Days { get; set; } = 28;
    public string Dimension { get; set; } = "query";
    public int Limit { get; set; } = 50;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// Aggregated search performance summary representing impressions, clicks, CTR, and positions.
/// </summary>
public class SearchPerformanceSummaryDto
{
    public int Days { get; set; } = 28;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public long TotalClicks { get; set; }
    public long TotalImpressions { get; set; }
    public double AverageCtr { get; set; }
    public double AveragePosition { get; set; }
    public double ClicksDeltaPercent { get; set; }
    public double ImpressionsDeltaPercent { get; set; }
    public string SiteUrl { get; set; } = "sc-domain:newsstand.ng";
    public string DataEngine { get; set; } = "LocalSimulatedEngine";

    public List<SearchAnalyticsDailyPointDto> DailyTrends { get; set; } = [];
    public List<SearchQueryPerformanceDto> TopQueries { get; set; } = [];
    public List<SearchPagePerformanceDto> TopPages { get; set; } = [];
    public List<SearchDevicePerformanceDto> DeviceBreakdown { get; set; } = [];
}

/// <summary>
/// Daily metrics time-series point.
/// </summary>
public class SearchAnalyticsDailyPointDto
{
    public DateTime Date { get; set; }
    public long Clicks { get; set; }
    public long Impressions { get; set; }
    public double Ctr { get; set; }
    public double Position { get; set; }
}

/// <summary>
/// Keyword / query-level ranking and performance.
/// </summary>
public class SearchQueryPerformanceDto
{
    public string Query { get; set; } = string.Empty;
    public long Clicks { get; set; }
    public long Impressions { get; set; }
    public double Ctr { get; set; }
    public double Position { get; set; }
}

/// <summary>
/// URL / page-level ranking and performance.
/// </summary>
public class SearchPagePerformanceDto
{
    public string Page { get; set; } = string.Empty;
    public string PageType { get; set; } = "NewsArticle"; // "DataTracker", "StateHub", "NewsArticle", "TopicHub", "StoryCluster", "Category"
    public long Clicks { get; set; }
    public long Impressions { get; set; }
    public double Ctr { get; set; }
    public double Position { get; set; }
}

/// <summary>
/// Device category distribution (Mobile, Desktop, Tablet).
/// </summary>
public class SearchDevicePerformanceDto
{
    public string Device { get; set; } = "MOBILE";
    public long Clicks { get; set; }
    public long Impressions { get; set; }
    public double Ctr { get; set; }
    public double PercentageShare { get; set; }
}

/// <summary>
/// Crawlability, indexation health, and sitemap diagnostics summary.
/// </summary>
public class CrawlDiagnosticsDto
{
    public int TotalDiscoveredUrls { get; set; }
    public int TotalIndexedUrls { get; set; }
    public int TotalExcludedUrls { get; set; }
    public int TotalCrawlErrors { get; set; }
    public double IndexationRate { get; set; }
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

    public List<IndexationBreakdownItemDto> IndexationBreakdown { get; set; } = [];
    public List<SitemapDiagnosticDto> Sitemaps { get; set; } = [];
    public List<CrawlIssueDto> CrawlIssues { get; set; } = [];
    public IndexNowDiagnosticsDto IndexNow { get; set; } = new();
}

/// <summary>
/// Itemized breakdown of URLs by search index status.
/// </summary>
public class IndexationBreakdownItemDto
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Description { get; set; } = string.Empty;
    public string BadgeVariant { get; set; } = "success";
}

/// <summary>
/// Diagnostic status for XML sitemaps.
/// </summary>
public class SitemapDiagnosticDto
{
    public string Name { get; set; } = string.Empty;
    public string Loc { get; set; } = string.Empty;
    public string Status { get; set; } = "SUCCESS"; // SUCCESS, WARNING, ERROR
    public int UrlCount { get; set; }
    public DateTime LastSubmitted { get; set; }
    public long ResponseTimeMs { get; set; }
    public string? Issues { get; set; }
}

/// <summary>
/// Actionable crawl anomaly or warning.
/// </summary>
public class CrawlIssueDto
{
    public string Url { get; set; } = string.Empty;
    public string IssueType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning"; // Info, Warning, Critical
    public string Message { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// IndexNow protocol status and metrics.
/// </summary>
public class IndexNowDiagnosticsDto
{
    public bool Enabled { get; set; } = true;
    public string Host { get; set; } = "newsstand.ng";
    public string Key { get; set; } = string.Empty;
    public int TotalSubmitted { get; set; }
    public DateTime? LastPingAt { get; set; }
    public string Status { get; set; } = "Active";
}

/// <summary>
/// Request to inspect a single URL via URL Inspection API.
/// </summary>
public class UrlInspectionRequestDto
{
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Comprehensive URL inspection result.
/// </summary>
public class UrlInspectionResultDto
{
    public string Url { get; set; } = string.Empty;
    public string NormalizedPath { get; set; } = string.Empty;
    public bool IsIndexed { get; set; }
    public string IndexingState { get; set; } = string.Empty;
    public string Verdict { get; set; } = "PASS"; // PASS, NEUTRAL, FAIL
    public string UserDeclaredCanonical { get; set; } = string.Empty;
    public string GoogleSelectedCanonical { get; set; } = string.Empty;
    public string MobileUsability { get; set; } = "Mobile friendly";
    public List<string> RichResults { get; set; } = [];
    public DateTime? LastCrawlTime { get; set; }
    public string CrawledAs { get; set; } = "Googlebot smartphone";
    public string PageFetchState { get; set; } = "Successful";
    public string RobotsTxtState { get; set; } = "Allowed";
    public List<string> Recommendations { get; set; } = [];
}
