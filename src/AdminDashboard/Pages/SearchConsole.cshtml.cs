using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;
using System.Text.Json;

namespace AdminDashboard.Pages;

public class SearchConsoleModel(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<SearchConsoleModel> logger) : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<SearchConsoleModel> _logger = logger;

    [BindProperty(SupportsGet = true)]
    public int Days { get; set; } = 28;

    [BindProperty(SupportsGet = true)]
    public string Dimension { get; set; } = "query";

    [BindProperty]
    public string InspectUrlInput { get; set; } = string.Empty;

    public SearchPerformanceSummaryDto? Performance { get; set; }
    public CrawlDiagnosticsDto? Diagnostics { get; set; }
    public UrlInspectionResultDto? InspectionResult { get; set; }
    public SeoHealthScorecardDto? HealthScorecard { get; set; }
    public List<SeoAnomalyAlertDto> AnomalyAlerts { get; set; } = [];
    public List<CtrOpportunityDto> CtrOpportunities { get; set; } = [];
    public AutomatedRemediationLogDto? LastRemediationResult { get; set; }

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadDashboardDataAsync();
    }

    public async Task<IActionResult> OnPostInspectUrlAsync()
    {
        await LoadDashboardDataAsync();

        if (string.IsNullOrWhiteSpace(InspectUrlInput))
        {
            ErrorMessage = "Please enter a valid article URL or site path to inspect.";
            return Page();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("NewsApiClient");
            var apiBase = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var response = await client.PostAsJsonAsync(
                $"{apiBase.TrimEnd('/')}/api/v1/seo/inspect-url",
                new { Url = InspectUrlInput.Trim() });

            if (response.IsSuccessStatusCode)
            {
                InspectionResult = await response.Content.ReadFromJsonAsync<UrlInspectionResultDto>(opts);
                SuccessMessage = $"URL inspection completed for {InspectUrlInput.Trim()}";
            }
            else
            {
                ErrorMessage = $"Failed to inspect URL (HTTP {response.StatusCode}).";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inspecting URL {Url}", InspectUrlInput);
            ErrorMessage = "Unable to connect to the NewsApi service to inspect URL.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostTriggerRemediationAsync(string actionType = "BackfillEnrichment", string? target = null)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NewsApiClient");
            var apiBase = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var response = await client.PostAsJsonAsync(
                $"{apiBase.TrimEnd('/')}/api/v1/seo/trigger-remediation",
                new { ActionType = actionType, Target = target });

            if (response.IsSuccessStatusCode)
            {
                LastRemediationResult = await response.Content.ReadFromJsonAsync<AutomatedRemediationLogDto>(opts);
                SuccessMessage = $"Autonomous Remediation [{actionType}] executed successfully! Status: {LastRemediationResult?.Status} - {LastRemediationResult?.Details}";
            }
            else
            {
                ErrorMessage = $"Failed to execute remediation action (HTTP {response.StatusCode}).";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering remediation {ActionType}", actionType);
            ErrorMessage = "Unable to connect to NewsApi to trigger remediation.";
        }

        await LoadDashboardDataAsync();
        return Page();
    }

    private async Task LoadDashboardDataAsync()
    {
        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBase = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // 1. Fetch Search Analytics
        try
        {
            var analyticsResponse = await client.GetAsync(
                $"{apiBase.TrimEnd('/')}/api/v1/seo/search-analytics?days={Days}&dimension={Dimension}");

            if (analyticsResponse.IsSuccessStatusCode)
            {
                Performance = await analyticsResponse.Content.ReadFromJsonAsync<SearchPerformanceSummaryDto>(opts);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load Search Analytics from NewsApi.");
            ErrorMessage = "Could not load Google Search Console analytics. Verify NewsApi is running.";
        }

        // 2. Fetch Crawl Diagnostics
        try
        {
            var diagnosticsResponse = await client.GetAsync(
                $"{apiBase.TrimEnd('/')}/api/v1/seo/crawl-diagnostics");

            if (diagnosticsResponse.IsSuccessStatusCode)
            {
                Diagnostics = await diagnosticsResponse.Content.ReadFromJsonAsync<CrawlDiagnosticsDto>(opts);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load Crawl Diagnostics from NewsApi.");
        }

        // 3. Fetch SEO Health Scorecard
        try
        {
            var scorecardResponse = await client.GetAsync(
                $"{apiBase.TrimEnd('/')}/api/v1/seo/health-scorecard");

            if (scorecardResponse.IsSuccessStatusCode)
            {
                HealthScorecard = await scorecardResponse.Content.ReadFromJsonAsync<SeoHealthScorecardDto>(opts);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load SEO Health Scorecard from NewsApi.");
        }

        // 4. Fetch Active ML Anomalies
        try
        {
            var anomaliesResponse = await client.GetAsync(
                $"{apiBase.TrimEnd('/')}/api/v1/seo/anomalies");

            if (anomaliesResponse.IsSuccessStatusCode)
            {
                AnomalyAlerts = await anomaliesResponse.Content.ReadFromJsonAsync<List<SeoAnomalyAlertDto>>(opts) ?? [];
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load SEO Anomalies from NewsApi.");
        }

        // 5. Fetch CTR Opportunities
        try
        {
            var oppResponse = await client.GetAsync(
                $"{apiBase.TrimEnd('/')}/api/v1/seo/ctr-opportunities");

            if (oppResponse.IsSuccessStatusCode)
            {
                CtrOpportunities = await oppResponse.Content.ReadFromJsonAsync<List<CtrOpportunityDto>>(opts) ?? [];
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load CTR Opportunities from NewsApi.");
        }
    }
}

// ──────────────────────────────────────────────────────────
// Mirror DTOs for Admin Dashboard
// ──────────────────────────────────────────────────────────

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
    public string SiteUrl { get; set; } = string.Empty;
    public string DataEngine { get; set; } = string.Empty;

    public List<SearchAnalyticsDailyPointDto> DailyTrends { get; set; } = [];
    public List<SearchQueryPerformanceDto> TopQueries { get; set; } = [];
    public List<SearchPagePerformanceDto> TopPages { get; set; } = [];
    public List<SearchDevicePerformanceDto> DeviceBreakdown { get; set; } = [];
}

public class SearchAnalyticsDailyPointDto
{
    public DateTime Date { get; set; }
    public long Clicks { get; set; }
    public long Impressions { get; set; }
    public double Ctr { get; set; }
    public double Position { get; set; }
}

public class SearchQueryPerformanceDto
{
    public string Query { get; set; } = string.Empty;
    public long Clicks { get; set; }
    public long Impressions { get; set; }
    public double Ctr { get; set; }
    public double Position { get; set; }
}

public class SearchPagePerformanceDto
{
    public string Page { get; set; } = string.Empty;
    public string PageType { get; set; } = "NewsArticle";
    public long Clicks { get; set; }
    public long Impressions { get; set; }
    public double Ctr { get; set; }
    public double Position { get; set; }
}

public class SearchDevicePerformanceDto
{
    public string Device { get; set; } = "MOBILE";
    public long Clicks { get; set; }
    public long Impressions { get; set; }
    public double Ctr { get; set; }
    public double PercentageShare { get; set; }
}

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

public class IndexationBreakdownItemDto
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Description { get; set; } = string.Empty;
    public string BadgeVariant { get; set; } = "success";
}

public class SitemapDiagnosticDto
{
    public string Name { get; set; } = string.Empty;
    public string Loc { get; set; } = string.Empty;
    public string Status { get; set; } = "SUCCESS";
    public int UrlCount { get; set; }
    public DateTime LastSubmitted { get; set; }
    public long ResponseTimeMs { get; set; }
    public string? Issues { get; set; }
}

public class CrawlIssueDto
{
    public string Url { get; set; } = string.Empty;
    public string IssueType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string Message { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

public class IndexNowDiagnosticsDto
{
    public bool Enabled { get; set; } = true;
    public string Host { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int TotalSubmitted { get; set; }
    public DateTime? LastPingAt { get; set; }
    public string Status { get; set; } = "Active";
}

public class UrlInspectionResultDto
{
    public string Url { get; set; } = string.Empty;
    public string NormalizedPath { get; set; } = string.Empty;
    public bool IsIndexed { get; set; }
    public string IndexingState { get; set; } = string.Empty;
    public string Verdict { get; set; } = "PASS";
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

public class SeoHealthScorecardDto
{
    public double OverallScore { get; set; } = 94.5;
    public string Grade { get; set; } = "EXCELLENT";
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    public HealthPillarDto IndexationHealth { get; set; } = new();
    public HealthPillarDto StructuredDataHealth { get; set; } = new();
    public HealthPillarDto SpeedAndCrawlHealth { get; set; } = new();
    public HealthPillarDto EeatAndComplianceHealth { get; set; } = new();
    public List<SeoAnomalyAlertDto> ActiveAlerts { get; set; } = [];
    public List<CtrOpportunityDto> CtrOpportunities { get; set; } = [];
    public List<AutomatedRemediationLogDto> RecentRemediations { get; set; } = [];
}

public class HealthPillarDto
{
    public string Name { get; set; } = string.Empty;
    public double Score { get; set; }
    public double Weight { get; set; } = 0.25;
    public string Status { get; set; } = "Passing";
    public string Summary { get; set; } = string.Empty;
}

public class SeoAnomalyAlertDto
{
    public string AlertId { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string TargetType { get; set; } = "Query";
    public string AnomalyType { get; set; } = "RankingDecay";
    public string Severity { get; set; } = "Warning";
    public double AnomalyScore { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public bool IsResolved { get; set; }
    public string? RecommendedAction { get; set; }
}

public class CtrOpportunityDto
{
    public string Query { get; set; } = string.Empty;
    public string PageUrl { get; set; } = string.Empty;
    public long Impressions { get; set; }
    public double ActualCtr { get; set; }
    public double PredictedBenchmarkCtr { get; set; }
    public double OpportunityGap { get; set; }
    public long EstimatedAdditionalClicks { get; set; }
    public string CurrentTitle { get; set; } = string.Empty;
    public List<string> AiHeadlineSuggestions { get; set; } = [];
    public List<FaqItemDto> SuggestedFaqPairs { get; set; } = [];
}

public class FaqItemDto
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}

public class AutomatedRemediationLogDto
{
    public string ActionType { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Status { get; set; } = "Completed";
    public string Details { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

