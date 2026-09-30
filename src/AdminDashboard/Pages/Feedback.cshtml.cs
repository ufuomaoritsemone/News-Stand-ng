using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;
using System.Text.Json;

namespace AdminDashboard.Pages;

public class FeedbackModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FeedbackModel> _logger;

    public FeedbackModel(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<FeedbackModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? SearchQuery { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedCategory { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? SelectedRating { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedPlatform { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Limit { get; set; } = 100;

    // Filtered items shown in UI
    public List<FeedbackItemDto> Feedbacks { get; set; } = [];

    // All fetched items (for global metrics)
    public int TotalFeedbacksCount { get; set; }
    public double AverageRating { get; set; }
    public int BugCount { get; set; }
    public int FeatureCount { get; set; }
    public int ContentCount { get; set; }
    public int GeneralCount { get; set; }
    public int EmailDispatchedCount { get; set; }
    public Dictionary<int, int> RatingCounts { get; set; } = new()
    {
        [5] = 0, [4] = 0, [3] = 0, [2] = 0, [1] = 0
    };
    public List<string> AvailablePlatforms { get; set; } = [];

    public async Task OnGetAsync()
    {
        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBase = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        int fetchLimit = Math.Clamp(Limit, 25, 200);

        try
        {
            var response = await client.GetAsync($"{apiBase.TrimEnd('/')}/api/v1/feedback?limit={fetchLimit}");
            if (response.IsSuccessStatusCode)
            {
                var allItems = await response.Content.ReadFromJsonAsync<List<FeedbackItemDto>>(opts) ?? [];

                // Calculate summary metrics on the full fetched dataset
                TotalFeedbacksCount = allItems.Count;
                if (allItems.Count > 0)
                {
                    AverageRating = Math.Round(allItems.Average(f => f.Rating), 1);
                    BugCount = allItems.Count(f => string.Equals(f.Category, "Bug", StringComparison.OrdinalIgnoreCase));
                    FeatureCount = allItems.Count(f => string.Equals(f.Category, "Feature", StringComparison.OrdinalIgnoreCase));
                    ContentCount = allItems.Count(f => string.Equals(f.Category, "Content", StringComparison.OrdinalIgnoreCase));
                    GeneralCount = allItems.Count(f => string.Equals(f.Category, "General", StringComparison.OrdinalIgnoreCase));
                    EmailDispatchedCount = allItems.Count(f => f.IsEmailSent);

                    for (int r = 1; r <= 5; r++)
                    {
                        RatingCounts[r] = allItems.Count(f => f.Rating == r);
                    }

                    AvailablePlatforms = allItems
                        .Where(f => !string.IsNullOrWhiteSpace(f.Platform))
                        .Select(f => f.Platform!.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(p => p)
                        .ToList();
                }

                // Apply in-memory filtering
                var query = allItems.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(SelectedCategory) && !string.Equals(SelectedCategory, "All", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(f => string.Equals(f.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase));
                }

                if (SelectedRating.HasValue && SelectedRating.Value > 0)
                {
                    query = query.Where(f => f.Rating == SelectedRating.Value);
                }

                if (!string.IsNullOrWhiteSpace(SelectedPlatform) && !string.Equals(SelectedPlatform, "All", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(f => string.Equals(f.Platform, SelectedPlatform, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    var term = SearchQuery.Trim();
                    query = query.Where(f =>
                        (f.Message != null && f.Message.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                        (f.UserEmail != null && f.UserEmail.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                        (f.UserName != null && f.UserName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                        (f.Category != null && f.Category.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                        (f.Id != null && f.Id.Contains(term, StringComparison.OrdinalIgnoreCase)));
                }

                Feedbacks = query.ToList();
            }
            else
            {
                TempData["Error"] = $"NewsApi returned status code {response.StatusCode} when fetching feedbacks.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to NewsApi at {ApiBase} to fetch feedbacks.", apiBase);
            TempData["Error"] = "Could not connect to NewsApi to fetch feedbacks. Please check if the API is running.";
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            TempData["Error"] = "Invalid feedback ID.";
            return RedirectToPage();
        }

        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBase = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";

        try
        {
            var response = await client.DeleteAsync($"{apiBase.TrimEnd('/')}/api/v1/feedback/{Uri.EscapeDataString(id)}");
            if (response.IsSuccessStatusCode)
            {
                TempData["Message"] = "Feedback deleted successfully.";
            }
            else
            {
                TempData["Error"] = $"Failed to delete feedback (Status: {response.StatusCode}).";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting feedback {FeedbackId}", id);
            TempData["Error"] = $"Error deleting feedback: {ex.Message}";
        }

        return RedirectToPage(new
        {
            SearchQuery,
            SelectedCategory,
            SelectedRating,
            SelectedPlatform,
            Limit
        });
    }
}

public class FeedbackItemDto
{
    public string Id { get; set; } = string.Empty;
    public int Rating { get; set; } = 5;
    public string Category { get; set; } = "General";
    public string Message { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public string? UserName { get; set; }
    public string? AppVersion { get; set; }
    public string? Platform { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsEmailSent { get; set; }
}
