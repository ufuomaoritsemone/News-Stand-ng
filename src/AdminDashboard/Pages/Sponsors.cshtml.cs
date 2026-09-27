using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;

namespace AdminDashboard.Pages;

public class SponsorsModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SponsorsModel> _logger;

    public SponsorsModel(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SponsorsModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [BindProperty]
    public SponsoredArticleInput NewCampaign { get; set; } = new();

    public List<SponsoredCampaignItem> Campaigns { get; set; } = [];

    public int TotalCampaigns => Campaigns.Count;
    public int ActiveCampaigns => Campaigns.Count(c => c.IsActive);
    public int TotalImpressions => Campaigns.Sum(c => c.ImpressionCount);
    public int TotalClicks => Campaigns.Sum(c => c.ClickCount);
    public double OverallCtr => TotalImpressions > 0
        ? Math.Round(((double)TotalClicks / TotalImpressions) * 100, 2)
        : 0;

    public async Task OnGetAsync()
    {
        await LoadDataAsync();
    }

    public async Task<IActionResult> OnPostCreateCampaignAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCampaign.Title) || string.IsNullOrWhiteSpace(NewCampaign.SponsorName))
        {
            TempData["Error"] = "Title and Sponsor Name are required.";
            await LoadDataAsync();
            return Page();
        }

        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";

        try
        {
            DateTime? expiresAt = null;
            if (NewCampaign.DurationDays > 0)
            {
                expiresAt = DateTime.UtcNow.AddDays(NewCampaign.DurationDays);
            }
            else if (NewCampaign.CustomExpiryDate.HasValue)
            {
                expiresAt = NewCampaign.CustomExpiryDate.Value.ToUniversalTime();
            }

            var request = new
            {
                Title = NewCampaign.Title.Trim(),
                Summary = NewCampaign.Summary?.Trim(),
                Content = NewCampaign.Content?.Trim(),
                Url = NewCampaign.Url?.Trim(),
                ImageUrl = NewCampaign.ImageUrl?.Trim(),
                SponsorName = NewCampaign.SponsorName.Trim(),
                SponsorUrl = NewCampaign.SponsorUrl?.Trim(),
                Category = !string.IsNullOrWhiteSpace(NewCampaign.Category) ? NewCampaign.Category.Trim() : "Business",
                CampaignExpiresAt = expiresAt,
                IsPinned = NewCampaign.IsPinned,
                TargetPosition = NewCampaign.TargetPosition,
                PriorityWeight = NewCampaign.PriorityWeight > 0 ? NewCampaign.PriorityWeight : 1
            };

            var response = await client.PostAsJsonAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/articles/sponsored", request);

            if (response.IsSuccessStatusCode)
            {
                TempData["Message"] = $"Sponsored campaign '{NewCampaign.Title}' created successfully!";
                return RedirectToPage();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                TempData["Error"] = $"Failed to create sponsored campaign: {response.StatusCode} - {error}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating sponsored campaign.");
            TempData["Error"] = $"Error connecting to News API: {ex.Message}";
        }

        await LoadDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostUpdatePlacementAsync(string id, int? targetPosition, int priorityWeight)
    {
        if (string.IsNullOrWhiteSpace(id)) return RedirectToPage();

        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";

        try
        {
            var update = new { TargetPosition = targetPosition, PriorityWeight = priorityWeight };
            var response = await client.PutAsJsonAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/articles/sponsored/{id}", update);

            if (response.IsSuccessStatusCode)
            {
                TempData["Message"] = "Campaign placement updated successfully!";
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                TempData["Error"] = $"Failed to update placement: {error}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating placement.");
            TempData["Error"] = $"Error connecting to News API: {ex.Message}";
        }

        return RedirectToPage();
    }

    public Dictionary<int, string> GetOccupiedSlots()
    {
        return Campaigns
            .Where(c => c.IsActive && c.TargetPosition.HasValue)
            .GroupBy(c => c.TargetPosition!.Value)
            .ToDictionary(g => g.Key, g => g.First().SponsorName ?? g.First().Title);
    }

    public async Task<IActionResult> OnPostTogglePinAsync(string id, bool currentPinned)
    {
        if (string.IsNullOrWhiteSpace(id)) return RedirectToPage();

        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";

        try
        {
            var update = new { IsPinned = !currentPinned };
            var response = await client.PutAsJsonAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/articles/sponsored/{id}", update);

            if (response.IsSuccessStatusCode)
            {
                TempData["Message"] = currentPinned
                    ? "Campaign unpinned."
                    : "Campaign pinned to top!";
                return RedirectToPage();
            }
            else
            {
                TempData["Error"] = "Failed to update pin status.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling campaign pin.");
            TempData["Error"] = $"Error connecting to News API: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(string id, bool currentActive)
    {
        if (string.IsNullOrWhiteSpace(id)) return RedirectToPage();

        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";

        try
        {
            var update = new { IsSponsored = !currentActive };
            var response = await client.PutAsJsonAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/articles/sponsored/{id}", update);

            if (response.IsSuccessStatusCode)
            {
                TempData["Message"] = currentActive
                    ? "Campaign paused/deactivated."
                    : "Campaign reactivated!";
                return RedirectToPage();
            }
            else
            {
                TempData["Error"] = "Failed to update campaign status.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling campaign active status.");
            TempData["Error"] = $"Error connecting to News API: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostExtendCampaignAsync(string id, int extraDays)
    {
        if (string.IsNullOrWhiteSpace(id)) return RedirectToPage();

        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";

        try
        {
            var newExpiry = DateTime.UtcNow.AddDays(Math.Max(1, extraDays));
            var update = new { CampaignExpiresAt = newExpiry, IsSponsored = true };
            var response = await client.PutAsJsonAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/articles/sponsored/{id}", update);

            if (response.IsSuccessStatusCode)
            {
                TempData["Message"] = $"Campaign extended by {extraDays} days (Expires {newExpiry:MMM d, yyyy}).";
            }
            else
            {
                TempData["Error"] = "Failed to extend campaign.";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"API error: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteCampaignAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return RedirectToPage();

        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";

        try
        {
            var response = await client.DeleteAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/articles/sponsored/{id}");
            if (response.IsSuccessStatusCode)
            {
                TempData["Message"] = "Campaign deleted successfully.";
                return RedirectToPage();
            }
            else
            {
                TempData["Error"] = "Failed to delete campaign.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting campaign.");
            TempData["Error"] = $"Error connecting to News API: {ex.Message}";
        }

        return RedirectToPage();
    }

    private async Task LoadDataAsync()
    {
        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";

        try
        {
            var result = await client.GetFromJsonAsync<List<SponsoredCampaignItem>>(
                $"{apiBaseUrl.TrimEnd('/')}/api/v1/articles/sponsored");

            Campaigns = result?
                .OrderByDescending(c => c.IsPinned)
                .ThenBy(c => c.TargetPosition ?? int.MaxValue)
                .ThenByDescending(c => c.PublishedAt)
                .ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load sponsored campaigns.");
            Campaigns = [];
        }
    }
}

public class SponsoredArticleInput
{
    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Summary { get; set; }
    public string? Content { get; set; }
    public string? Url { get; set; }
    public string? ImageUrl { get; set; }

    [Required]
    public string SponsorName { get; set; } = string.Empty;
    public string? SponsorUrl { get; set; }

    public string Category { get; set; } = "Business";

    public int DurationDays { get; set; } = 14;
    public DateTime? CustomExpiryDate { get; set; }

    public bool IsPinned { get; set; } = false;

    public int? TargetPosition { get; set; } = 5;
    public int PriorityWeight { get; set; } = 1;
}

public class SponsoredCampaignItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Content { get; set; }
    public string? Url { get; set; }
    public string? ImageUrl { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Category { get; set; } = "Business";
    public DateTime PublishedAt { get; set; }
    public bool IsSponsored { get; set; }
    public string? SponsorName { get; set; }
    public string? SponsorUrl { get; set; }
    public DateTime? CampaignExpiresAt { get; set; }
    public bool IsPinned { get; set; }
    public int? TargetPosition { get; set; }
    public int PriorityWeight { get; set; } = 1;
    public int ImpressionCount { get; set; }
    public int ClickCount { get; set; }
    public double ClickThroughRate => ImpressionCount > 0
        ? Math.Round(((double)ClickCount / ImpressionCount) * 100, 2)
        : 0;

    public bool IsSpecialPlacement => TargetPosition is >= 1 and <= 3;

    public string PlacementDisplay => TargetPosition switch
    {
        1 => "⭐ Special Slot #1",
        2 => "⭐ Special Slot #2",
        3 => "⭐ Special Slot #3",
        >= 5 and <= 30 => $"Slot #{TargetPosition}",
        _ => IsPinned ? "⭐ Special Slot #1 (Pinned)" : "Standard In-Feed"
    };

    public bool IsActive => IsSponsored && (CampaignExpiresAt == null || CampaignExpiresAt > DateTime.UtcNow);

    public string StatusText => !IsSponsored
        ? "Disabled"
        : (CampaignExpiresAt.HasValue && CampaignExpiresAt.Value <= DateTime.UtcNow)
            ? "Expired"
            : "Active";
}
