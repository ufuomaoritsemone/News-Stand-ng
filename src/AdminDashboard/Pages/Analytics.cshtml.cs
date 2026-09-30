using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;

namespace AdminDashboard.Pages;

public class AnalyticsModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public AnalyticsModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [BindProperty(SupportsGet = true)]
    public int Days { get; set; } = 30;

    public AnalyticsSummaryDto? Summary { get; set; }
    public List<RecentEventDto> RecentEvents { get; set; } = [];

    public async Task OnGetAsync()
    {
        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBase = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";
        var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Load summary
        try
        {
            var response = await client.GetAsync($"{apiBase.TrimEnd('/')}/api/v1/analytics/summary?days={Days}");
            if (response.IsSuccessStatusCode)
                Summary = await response.Content.ReadFromJsonAsync<AnalyticsSummaryDto>(opts);
        }
        catch
        {
            TempData["Error"] = "Could not connect to the NewsApi to fetch analytics. Is it running?";
        }

        // Load recent events
        try
        {
            var response = await client.GetAsync($"{apiBase.TrimEnd('/')}/api/v1/analytics/events?pageSize=30&page=1");
            if (response.IsSuccessStatusCode)
            {
                var paged = await response.Content.ReadFromJsonAsync<PagedEventsDto>(opts);
                RecentEvents = paged?.Rows ?? [];
            }
        }
        catch { }
    }
}

// ──────────────────────────────────────────────────────────
// DTOs (mirror of NewsApi AnalyticsController DTOs)
// ──────────────────────────────────────────────────────────

public class AnalyticsSummaryDto
{
    public int TotalEvents { get; set; }
    public int UniqueDevices { get; set; }
    public int AppOpens { get; set; }
    public int ArticleReads { get; set; }
    public int SharesClicked { get; set; }
    public int BookmarksAdded { get; set; }
    public int AudioListensStarted { get; set; }
    public int AudioListensCompleted { get; set; }
    public double AudioCompletionRate { get; set; }
    // Widget adoption metrics
    public int WidgetInstalls { get; set; }
    public int WidgetRemovals { get; set; }
    public int WidgetRefreshes { get; set; }
    public int WidgetClicks { get; set; }
    public int NetWidgetDeployments { get; set; }
    public int PeriodDays { get; set; }
    public List<TopArticleDto> TopArticles { get; set; } = [];
    public List<DailyCountDto> DailyBreakdown { get; set; } = [];
}

public class TopArticleDto
{
    public string ArticleId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int ReadCount { get; set; }
}

public class DailyCountDto
{
    public string Date { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class RecentEventDto
{
    public string Id { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? ArticleId { get; set; }
    public string? ArticleTitle { get; set; }
    public string? Category { get; set; }
    public string? Platform { get; set; }
    public string? AppVersion { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}

public class PagedEventsDto
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<RecentEventDto> Rows { get; set; } = [];
}
