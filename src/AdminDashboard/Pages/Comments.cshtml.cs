using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace AdminDashboard.Pages;

public class CommentsModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CommentsModel> _logger;

    public CommentsModel(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<CommentsModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? SearchQuery { get; set; }

    [BindProperty(SupportsGet = true)]
    public string FilterStatus { get; set; } = "All"; // "All", "Flagged", "Published"

    public List<CommentAdminItem> Comments { get; set; } = [];
    public int TotalCommentsCount { get; set; }
    public int FlaggedCount { get; set; }
    public int PublishedCount { get; set; }

    public async Task OnGetAsync()
    {
        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBase = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        try
        {
            var res = await client.GetAsync($"{apiBase}/api/v1/comments/flagged");
            if (res.IsSuccessStatusCode)
            {
                var content = await res.Content.ReadAsStringAsync();
                var items = JsonSerializer.Deserialize<List<CommentAdminItem>>(content, opts) ?? [];
                
                TotalCommentsCount = items.Count;
                FlaggedCount = items.Count(c => c.Status == "Flagged" || c.ReportCount > 0);
                PublishedCount = items.Count(c => c.Status == "Published");

                if (FilterStatus == "Flagged")
                {
                    items = items.Where(c => c.Status == "Flagged" || c.ReportCount > 0).ToList();
                }
                else if (FilterStatus == "Published")
                {
                    items = items.Where(c => c.Status == "Published").ToList();
                }

                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    var q = SearchQuery.Trim().ToLowerInvariant();
                    items = items.Where(c =>
                        (c.AuthorName?.ToLowerInvariant().Contains(q) == true) ||
                        (c.Content?.ToLowerInvariant().Contains(q) == true) ||
                        (c.ArticleTitle?.ToLowerInvariant().Contains(q) == true)
                    ).ToList();
                }

                Comments = items;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch comments for moderation in AdminDashboard");
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            TempData["Error"] = "Invalid comment ID.";
            return RedirectToPage();
        }

        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBase = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";

        try
        {
            var res = await client.DeleteAsync($"{apiBase}/api/v1/comments/{id}");
            if (res.IsSuccessStatusCode)
            {
                TempData["Message"] = "Comment was removed by moderator.";
            }
            else
            {
                TempData["Error"] = "Failed to remove comment.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove comment {Id}", id);
            TempData["Error"] = "Error connecting to NewsApi.";
        }

        return RedirectToPage();
    }
}

public class CommentAdminItem
{
    public string Id { get; set; } = string.Empty;
    public string ArticleId { get; set; } = string.Empty;
    public string? ArticleTitle { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Upvotes { get; set; }
    public int Downvotes { get; set; }
    public int ReportCount { get; set; }
    public bool IsPinned { get; set; }
    public string Status { get; set; } = "Published";
    public DateTime CreatedAt { get; set; }
}
