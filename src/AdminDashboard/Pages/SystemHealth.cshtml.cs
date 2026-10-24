using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminDashboard.Pages;

public class SystemHealthModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SystemHealthModel> _logger;

    public SystemHealthModel(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SystemHealthModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public SystemHealthReportDto? HealthReport { get; set; }
    public bool LivenessProbeSuccess { get; set; }
    public int LivenessStatusCode { get; set; }
    public bool ReadinessProbeSuccess { get; set; }
    public int ReadinessStatusCode { get; set; }
    public string? ErrorMessage { get; set; }

    [TempData]
    public string? ActionFeedback { get; set; }

    [TempData]
    public string? ActionFeedbackType { get; set; } // "success", "danger", "warning", "info"

    public async Task OnGetAsync()
    {
        await LoadHealthStatusAsync();
    }

    public async Task<IActionResult> OnPostPingDatabaseAsync()
    {
        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBase = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";

        try
        {
            var response = await client.PostAsync($"{apiBase.TrimEnd('/')}/Health/ping-db", null);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<PingDbResponse>();
                ActionFeedback = result?.Success == true
                    ? $"Database probe successful! Response received in {result.LatencyMs} ms ({result.Provider})."
                    : $"Database probe warning: {result?.Error ?? "Could not connect"}";
                ActionFeedbackType = result?.Success == true ? "success" : "danger";
            }
            else
            {
                ActionFeedback = $"Database ping failed with HTTP {(int)response.StatusCode}.";
                ActionFeedbackType = "danger";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ping database");
            ActionFeedback = $"Network error executing database ping: {ex.Message}";
            ActionFeedbackType = "danger";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPurgeCacheAsync()
    {
        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBase = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";

        try
        {
            var response = await client.PostAsync($"{apiBase.TrimEnd('/')}/Health/purge-cache", null);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<PurgeCacheResponse>();
                ActionFeedback = result?.Message ?? "Cache purged successfully.";
                ActionFeedbackType = "success";
            }
            else
            {
                ActionFeedback = $"Cache purge failed with HTTP {(int)response.StatusCode}.";
                ActionFeedbackType = "danger";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to purge cache");
            ActionFeedback = $"Network error communicating with API: {ex.Message}";
            ActionFeedbackType = "danger";
        }

        return RedirectToPage();
    }

    private async Task LoadHealthStatusAsync()
    {
        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBase = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";

        // 1. Check Liveness Probe
        try
        {
            var livenessResp = await client.GetAsync($"{apiBase.TrimEnd('/')}/healthz/liveness");
            LivenessStatusCode = (int)livenessResp.StatusCode;
            LivenessProbeSuccess = livenessResp.IsSuccessStatusCode;
        }
        catch
        {
            LivenessProbeSuccess = false;
            LivenessStatusCode = 0;
        }

        // 2. Check Readiness Probe
        try
        {
            var readinessResp = await client.GetAsync($"{apiBase.TrimEnd('/')}/healthz/readiness");
            ReadinessStatusCode = (int)readinessResp.StatusCode;
            ReadinessProbeSuccess = readinessResp.IsSuccessStatusCode;
        }
        catch
        {
            ReadinessProbeSuccess = false;
            ReadinessStatusCode = 0;
        }

        // 3. Load Detailed Telemetry Report
        try
        {
            var response = await client.GetAsync($"{apiBase.TrimEnd('/')}/Health/details");
            if (response.IsSuccessStatusCode)
            {
                HealthReport = await response.Content.ReadFromJsonAsync<SystemHealthReportDto>();
            }
            else
            {
                ErrorMessage = $"Failed to fetch detailed telemetry: HTTP {(int)response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching detailed health report from NewsApi");
            ErrorMessage = $"Unable to reach NewsApi at {apiBase}: {ex.Message}";
        }
    }
}

public record SystemHealthReportDto(
    string Status,
    DateTimeOffset ServerTimeUtc,
    string Environment,
    ProcessHealthDto Process,
    DatabaseHealthDetailsDto Database,
    WorkersHealthDetailsDto Workers,
    StorageHealthDetailsDto Storage,
    AiEnrichmentHealthDto? AiEnrichment = null
);

public record AiEnrichmentHealthDto(
    bool Configured,
    bool CloudAiAvailable,
    bool CircuitBreakerActive,
    DateTime? CircuitBreakerUntilUtc,
    string Model,
    string FallbackEngine
);

public record ProcessHealthDto(
    int ProcessId,
    long UptimeSeconds,
    string UptimeFormatted,
    DateTimeOffset StartTimeUtc,
    double WorkingSetMb,
    double GcAllocatedMb,
    string DotNetVersion,
    string OsDescription,
    string Architecture
);

public record DatabaseHealthDetailsDto(
    string Provider,
    bool Connected,
    string? Error,
    long LatencyMs,
    int TotalArticles,
    int TotalSources,
    int TotalComments,
    int TotalFeedbacks
);

public record WorkersHealthDetailsDto(
    string ScraperStatus,
    DateTime? LatestArticleAt,
    string? LatestArticleTitle,
    DateTime? FxLastUpdated
);

public record StorageHealthDetailsDto(
    StorageFolderDetailsDto ImageProxyCache,
    StorageFolderDetailsDto AudioStorage
);

public record StorageFolderDetailsDto(
    int FileCount,
    double SizeMb,
    string Directory
);

public record PingDbResponse(
    bool Success,
    long LatencyMs,
    string? Provider,
    string? Message,
    string? Error
);

public record PurgeCacheResponse(
    bool Success,
    string Message,
    int FilesDeleted,
    double FreedMb
);
