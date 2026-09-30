using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;

namespace AdminDashboard.Pages;

public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public IndexModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [BindProperty]
    public SourceInput NewSource { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string SelectedChannel { get; set; } = "All";

    [BindProperty(SupportsGet = true)]
    public string SelectedCategory { get; set; } = "All";

    [BindProperty(SupportsGet = true)]
    public string SearchQuery { get; set; } = string.Empty;

    public List<SourceDto> Sources { get; set; } = new();
    public List<ArticleDto> Articles { get; set; } = new();
    public List<string> Channels { get; set; } = new();
    public List<string> Categories { get; set; } = new();

    public int TotalArticlesCount { get; set; }
    public int TodayArticlesCount { get; set; }
    public int AudioCount { get; set; }
    public bool IsUpToDate { get; set; }

    public async Task OnGetAsync()
    {
        ModelState.Clear();
        await LoadDataAsync();
    }

    public async Task<IActionResult> OnPostAddSourceAsync()
    {
        if (string.IsNullOrWhiteSpace(NewSource.Name))
        {
            ModelState.AddModelError("NewSource.Name", "Source name is required.");
            await LoadDataAsync();
            return Page();
        }

        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";
        try
        {
            var response = await client.PostAsJsonAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/sources", new 
            { 
                NewSource.Name, 
                NewSource.RssUrl,
                NewSource.SitemapUrl,
                NewSource.ScraperType
            });
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AddSourceResponseDto>();
                var count = result?.ScrapedCount ?? 0;
                TempData["Message"] = $"Source '{NewSource.Name}' added successfully! Actively scraped {count} new article(s).";
            }
            else
            {
                TempData["Error"] = "Failed to add source.";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error connecting to API: {ex.Message}";
        }

        return RedirectToPage(new { SelectedChannel = NewSource.Name, SelectedCategory = "All", SearchQuery = "" });
    }

    public async Task<IActionResult> OnPostScrapeSourceAsync(string id)
    {
        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";
        try
        {
            var response = await client.PostAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/sources/{id}/scrape", null);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ScrapeResultDto>();
                TempData["Message"] = $"Feed re-scraped successfully! Fetched {result?.ScrapedCount ?? 0} new article(s).";
            }
            else
            {
                TempData["Error"] = "Failed to scrape source feed.";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error: {ex.Message}";
        }

        return RedirectToPage(new { SelectedChannel, SelectedCategory, SearchQuery });
    }

    public async Task<IActionResult> OnPostRemoveSourceAsync(string id)
    {
        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";
        try
        {
            var response = await client.DeleteAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/sources/{id}");
            if (response.IsSuccessStatusCode)
            {
                TempData["Message"] = "Source removed.";
            }
            else
            {
                TempData["Error"] = "Failed to remove source.";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error: {ex.Message}";
        }

        return RedirectToPage(new { SelectedChannel, SelectedCategory, SearchQuery });
    }

    public async Task<IActionResult> OnPostUpdateCategoryAsync(string articleId, string newCategory)
    {
        if (string.IsNullOrWhiteSpace(articleId) || string.IsNullOrWhiteSpace(newCategory))
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return new JsonResult(new { success = false, message = "Article ID and new category are required." });
            }
            TempData["Error"] = "Article ID and new category are required.";
            return RedirectToPage(new { SelectedChannel, SelectedCategory, SearchQuery });
        }

        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";
        try
        {
            var response = await client.PutAsJsonAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/articles/{articleId}/category", new { category = newCategory });
            if (response.IsSuccessStatusCode)
            {
                var msg = $"Category updated to '{newCategory}'! Saved to ML training dataset.";
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return new JsonResult(new { success = true, message = msg });
                }
                TempData["Message"] = msg;
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                var msg = !string.IsNullOrWhiteSpace(errorBody) ? errorBody : response.ReasonPhrase ?? "Unknown error";
                var errorMsg = $"Failed to update article category ({response.StatusCode}): {msg}";
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return new JsonResult(new { success = false, message = errorMsg });
                }
                TempData["Error"] = errorMsg;
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error updating category: {ex.Message}";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return new JsonResult(new { success = false, message = errorMsg });
            }
            TempData["Error"] = errorMsg;
        }

        return RedirectToPage(new { SelectedChannel, SelectedCategory, SearchQuery });
    }

    private async Task LoadDataAsync()
    {
        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";

        // Load Sources
        try
        {
            var response = await client.GetAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/sources");
            if (response.IsSuccessStatusCode)
            {
                Sources = (await response.Content.ReadFromJsonAsync<List<SourceDto>>()) ?? new();
            }
        }
        catch
        {
            Sources = new();
        }

        // Load Articles (Archived & Today)
        try
        {
            var url = $"{apiBaseUrl.TrimEnd('/')}/api/v1/articles?limit=500&todayOnly=false";
            if (!string.IsNullOrWhiteSpace(SelectedChannel) && SelectedChannel != "All")
            {
                url += $"&source={Uri.EscapeDataString(SelectedChannel)}";
            }
            if (!string.IsNullOrWhiteSpace(SelectedCategory) && SelectedCategory != "All")
            {
                url += $"&category={Uri.EscapeDataString(SelectedCategory)}";
            }
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                url += $"&search={Uri.EscapeDataString(SearchQuery)}";
            }

            var response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var jsonOpts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var fetched = (await response.Content.ReadFromJsonAsync<List<ArticleDto>>(jsonOpts)) ?? new();
                
                // Clean HTML tags from Titles & Summaries
                foreach (var a in fetched)
                {
                    a.Title = CleanHtml(a.Title);
                    a.Summary = CleanHtml(a.Summary ?? string.Empty);
                }

                Articles = fetched;
            }
        }
        catch
        {
            Articles = new();
        }

        // Compute metrics and storage freshness status
        TotalArticlesCount = Articles.Count;
        var today = DateTime.UtcNow.Date;
        TodayArticlesCount = Articles.Count(a => a.PublishedAt.Date == today);
        IsUpToDate = TodayArticlesCount > 0;
        AudioCount = Articles.Count(a => !string.IsNullOrEmpty(a.AudioUrl));

        // Available Channel List for tabs/filters
        var knownChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "All" };
        foreach (var s in Sources)
        {
            if (!string.IsNullOrWhiteSpace(s.Name)) knownChannels.Add(s.Name);
        }
        foreach (var a in Articles)
        {
            if (!string.IsNullOrWhiteSpace(a.Source)) knownChannels.Add(a.Source);
        }
        Channels = knownChannels.ToList();

        // Available Categories
        var knownCats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "All", "Politics", "Business", "Sports", "Entertainment", "Technology", "Crime", "International", "General"
        };
        foreach (var a in Articles)
        {
            if (!string.IsNullOrWhiteSpace(a.Category)) knownCats.Add(a.Category);
        }
        Categories = knownCats.ToList();
    }

    public static string CleanHtml(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;
        string clean = System.Text.RegularExpressions.Regex.Replace(rawText, "<.*?>", string.Empty);
        return System.Net.WebUtility.HtmlDecode(clean).Trim();
    }

    private class AddSourceResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? RssUrl { get; set; }
        public int ScrapedCount { get; set; }
    }

    private class ScrapeResultDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int ScrapedCount { get; set; }
    }
}

public class SourceInput
{
    public string Name { get; set; } = string.Empty;
    public string? RssUrl { get; set; }
    public string? SitemapUrl { get; set; }
    public string? ScraperType { get; set; } = "Hybrid";
}

public class SourceDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? RssUrl { get; set; }
    public string? SitemapUrl { get; set; }
    public string? ScraperType { get; set; } = "Hybrid";
}

public class ArticleDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Content { get; set; }
    public string? Url { get; set; }
    public string? ImageUrl { get; set; }
    public string Source { get; set; } = "General";
    public string Category { get; set; } = "General";
    public DateTime PublishedAt { get; set; }
    public string? AudioUrl { get; set; }
}
