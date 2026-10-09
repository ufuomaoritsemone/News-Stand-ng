using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;
using System.Text.Json;

namespace AdminDashboard.Pages;

public class SettingsModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SettingsModel> _logger;

    public SettingsModel(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SettingsModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public SystemDiagnosticsDto Diagnostics { get; set; } = new(
        DbProvider: "SQLite",
        DbStatus: "Checking...",
        TotalArticles: 0,
        TotalSources: 0,
        TotalFeedbacks: 0,
        TotalComments: 0,
        EnvironmentName: "Development",
        ServerTimeUtc: DateTimeOffset.UtcNow,
        AppVersion: "1.0.0"
    );

    public Dictionary<string, string?> SettingsDict { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<SystemSettingItemDto> SettingsList { get; set; } = [];

    [BindProperty]
    public Dictionary<string, string?> FormInputs { get; set; } = [];

    [BindProperty]
    public string? TestApiKey { get; set; }

    [BindProperty]
    public string? TestEmailTarget { get; set; }

    public string? TestResultStatus { get; set; }
    public string? TestResultMessage { get; set; }
    public bool? TestResultSuccess { get; set; }

    public async Task OnGetAsync()
    {
        await LoadSettingsAsync();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBase = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";

        try
        {
            var updatePayload = new { Settings = FormInputs };
            var response = await client.PutAsJsonAsync($"{apiBase.TrimEnd('/')}/api/v1/settings", updatePayload);

            if (response.IsSuccessStatusCode)
            {
                TempData["Message"] = "System configuration settings updated and hot-reloaded successfully!";
            }
            else
            {
                var err = await response.Content.ReadAsStringAsync();
                TempData["Error"] = $"Failed to save settings: HTTP {(int)response.StatusCode} - {err}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings to API");
            TempData["Error"] = $"Network error communicating with API: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTestGeminiAsync()
    {
        await LoadSettingsAsync();
        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBase = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";

        try
        {
            var payload = new { ApiKey = TestApiKey };
            var response = await client.PostAsJsonAsync($"{apiBase.TrimEnd('/')}/api/v1/settings/test-gemini", payload);
            var result = await response.Content.ReadFromJsonAsync<TestConnectionResponseDto>();

            TestResultSuccess = result?.Success ?? false;
            TestResultMessage = result?.Message ?? "No response received.";
            TestResultStatus = "Gemini";
        }
        catch (Exception ex)
        {
            TestResultSuccess = false;
            TestResultMessage = $"Error connecting to API test endpoint: {ex.Message}";
            TestResultStatus = "Gemini";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostTestYouTubeAsync()
    {
        await LoadSettingsAsync();
        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBase = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";

        try
        {
            var payload = new { ApiKey = TestApiKey };
            var response = await client.PostAsJsonAsync($"{apiBase.TrimEnd('/')}/api/v1/settings/test-youtube", payload);
            var result = await response.Content.ReadFromJsonAsync<TestConnectionResponseDto>();

            TestResultSuccess = result?.Success ?? false;
            TestResultMessage = result?.Message ?? "No response received.";
            TestResultStatus = "YouTube";
        }
        catch (Exception ex)
        {
            TestResultSuccess = false;
            TestResultMessage = $"Error connecting to API test endpoint: {ex.Message}";
            TestResultStatus = "YouTube";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostTestSmtpAsync()
    {
        await LoadSettingsAsync();
        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBase = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";

        try
        {
            var payload = new { Target = TestEmailTarget };
            var response = await client.PostAsJsonAsync($"{apiBase.TrimEnd('/')}/api/v1/settings/test-smtp", payload);
            var result = await response.Content.ReadFromJsonAsync<TestConnectionResponseDto>();

            TestResultSuccess = result?.Success ?? false;
            TestResultMessage = result?.Message ?? "No response received.";
            TestResultStatus = "SMTP";
        }
        catch (Exception ex)
        {
            TestResultSuccess = false;
            TestResultMessage = $"Error testing SMTP: {ex.Message}";
            TestResultStatus = "SMTP";
        }

        return Page();
    }

    private async Task LoadSettingsAsync()
    {
        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBase = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";

        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var response = await client.GetFromJsonAsync<SystemSettingsResponseDto>($"{apiBase.TrimEnd('/')}/api/v1/settings", opts);

            if (response != null)
            {
                Diagnostics = response.Diagnostics;
                SettingsList = response.Settings;
                SettingsDict = response.Settings.ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load system settings from API");
            TempData["Error"] = $"Unable to load settings from API at {apiBase}: {ex.Message}";
        }
    }

    public string GetVal(string key, string fallback = "")
    {
        if (SettingsDict.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
        {
            return val;
        }
        return fallback;
    }

    public bool GetBool(string key, bool fallback = false)
    {
        var val = GetVal(key);
        return bool.TryParse(val, out var res) ? res : fallback;
    }
}

public record SystemDiagnosticsDto(
    string DbProvider,
    string DbStatus,
    int TotalArticles,
    int TotalSources,
    int TotalFeedbacks,
    int TotalComments,
    string EnvironmentName,
    DateTimeOffset ServerTimeUtc,
    string AppVersion
);

public record SystemSettingItemDto(
    string Key,
    string? Value,
    string Category,
    string? Description,
    bool IsSecret,
    bool IsConfigured,
    DateTimeOffset UpdatedAt
);

public record SystemSettingsResponseDto(
    SystemDiagnosticsDto Diagnostics,
    List<SystemSettingItemDto> Settings
);

public record TestConnectionResponseDto(
    bool Success,
    string Message,
    int? StatusCode = null
);
